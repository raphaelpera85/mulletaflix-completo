package org.mulletaflix.android.service

import android.content.Context
import android.net.Uri
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.media3.exoplayer.offline.DownloadService as Media3DownloadService
import androidx.media3.exoplayer.scheduler.Requirements
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.Job
import kotlinx.coroutines.joinAll
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import okhttp3.Call
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.CleartextTrafficPolicy
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.media.retargetMediaUrl
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import org.mulletaflix.domain.repository.DownloadMediaMetadata
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import org.mulletaflix.domain.repository.DownloadState
import org.mulletaflix.domain.repository.downloadServerScopeId
import java.io.File
import java.io.IOException
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import java.util.concurrent.TimeUnit
import java.util.concurrent.ConcurrentHashMap
import javax.inject.Inject
import javax.inject.Singleton

@UnstableApi
@Singleton
class Media3DownloadRepository @Inject constructor(
    @param:ApplicationContext private val appContext: Context,
    private val sessionRepository: SessionRepository,
    clientIdentityInterceptor: ClientIdentityInterceptor,
    private val subtitleRecoveryWorkScheduler: OfflineSubtitleRecoveryWorkScheduler,
) : DownloadRepository {
    private val manager = DownloadManagerSingleton.get(appContext)
    private val metadata = appContext.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
    private val titles = ConcurrentHashMap<String, String>()
    private val queuePaused = MutableStateFlow(metadata.getBoolean(KEY_QUEUE_PAUSED, false))
    private val wifiOnly = MutableStateFlow(metadata.getBoolean(KEY_WIFI_ONLY, false))
    private val repositoryScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val artworkStore = OfflineArtworkStore(File(appContext.filesDir, "offline_artwork"))
    private val subtitleStore = OfflineSubtitleStore(File(appContext.filesDir, "offline_subtitles"))
    private val artworkClient = OkHttpClient.Builder()
        .addInterceptor(clientIdentityInterceptor)
        .enforceLocalNetworkCleartextPolicy()
        .addNetworkInterceptor(clientIdentityInterceptor.identityAfterConnectedRoute())
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()
    private val subtitleClient = offlineSubtitleHttpClient(
        clientIdentityInterceptor,
        clientIdentityInterceptor.identityAfterConnectedRoute(),
    )
    private val artworkPersistenceLock = Any()
    private val artworkUpdates = MutableSharedFlow<Unit>(replay = 1, extraBufferCapacity = 1)
    private val artworkRequestsStarted = ConcurrentHashMap.newKeySet<String>()
    private val subtitleUpdates = MutableSharedFlow<Unit>(replay = 1, extraBufferCapacity = 1)
    private val subtitleRequestsStarted = ConcurrentHashMap.newKeySet<String>()
    private val subtitleJobs = ConcurrentHashMap<String, Job>()
    private val subtitleCalls = ConcurrentHashMap<String, MutableSet<Call>>()
    private val subtitleSessions = ConcurrentHashMap<String, FeedbackRequestSession>()
    private val subtitleRetryableFailures = ConcurrentHashMap.newKeySet<String>()
    private val subtitleJobsLock = Any()
    private val subtitlePersistenceLock = Any()
    @Volatile private var currentUserId: String? = null
    @Volatile private var currentServerId: String? = null

    /**
     * Server address and token as they are **now**.
     *
     * A download URL is written into the Media3 index and outlives the address it was
     * built with. Without these, "Tentar novamente" re-added the stored URL verbatim,
     * so a download that failed at home kept being retried against the LAN address
     * after the app had switched to the public one — and against the token from that
     * session.
     */
    @Volatile private var currentBaseUrl: String = ""
    @Volatile private var currentAccessToken: String? = null

    init {
        manager.requirements = requirementsFor(wifiOnly.value)
        if (queuePaused.value) pauseDownloadsThroughService()
        repositoryScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { currentUserId = it }
        }
        repositoryScope.launch {
            sessionRepository.getServerId().distinctUntilChanged().collect { currentServerId = it }
        }
        repositoryScope.launch {
            sessionRepository.getBaseUrl().distinctUntilChanged().collect { currentBaseUrl = it }
        }
        repositoryScope.launch {
            sessionRepository.getAccessToken().distinctUntilChanged().collect { currentAccessToken = it }
        }
        repositoryScope.launch {
            sessionRepository.getFeedbackRequestSession().distinctUntilChanged().collect { session ->
                subtitleSessions.entries.toList()
                    .filter { (_, activeSession) -> session == null || activeSession != session }
                    .mapNotNull { (requestId, _) -> cancelSubtitleFetch(requestId) }
                    .joinAll()
            }
        }
        // A queue left over from a previous run is only driven while something
        // tells the DownloadService to run. It used to be nobody: the service is
        // declared in the manifest and implements its notification, but the
        // repository talked to the DownloadManager directly, so no service was
        // ever started — no ongoing notification, and a queued or partial
        // download simply stopped when the app process died.
        //
        // Só quando a fila não está pausada. Este pedido de retomada é
        // incondicional e roda no início de todo processo: uma fila que o usuário
        // pausou de propósito voltava a baixar sozinha a cada vez que o app
        // abria, gastando dados móveis contra a intenção declarada, enquanto a
        // tela continuava dizendo "pausado".
        if (!queuePaused.value) {
            resumeDownloadsThroughService()
        }
    }

    override fun observeWifiOnly(): Flow<Boolean> = wifiOnly

    override fun observeQueuePaused(): Flow<Boolean> = queuePaused

    override fun setWifiOnly(enabled: Boolean): Result<Unit> = runCatching {
        manager.requirements = requirementsFor(enabled)
        metadata.edit().putBoolean(KEY_WIFI_ONLY, enabled).apply()
        wifiOnly.value = enabled
    }

    override fun observeDownloads(): Flow<List<DownloadEntry>> = callbackFlow {
        fun emitSnapshot() { trySend(snapshot()) }
        val artworkJob = launch { artworkUpdates.collect { emitSnapshot() } }
        val subtitleJob = launch { subtitleUpdates.collect { emitSnapshot() } }
        val sessionJob = launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect {
                currentUserId = it
                emitSnapshot()
            }
        }
        val serverJob = launch {
            sessionRepository.getServerId().distinctUntilChanged().collect {
                currentServerId = it
                emitSnapshot()
            }
        }
        val baseUrlJob = launch {
            sessionRepository.getBaseUrl().distinctUntilChanged().collect {
                currentBaseUrl = it
                emitSnapshot()
            }
        }
        val listener = object : DownloadManager.Listener {
            override fun onDownloadChanged(downloadManager: DownloadManager, download: Download, finalException: Exception?) = emitSnapshot()
            override fun onDownloadRemoved(downloadManager: DownloadManager, download: Download) {
                cleanupOfflineSubtitles(download.request)
                clearDownloadFailure(metadata, download.request.id)
                emitSnapshot()
            }
        }
        manager.addListener(listener)
        emitSnapshot()
        awaitClose {
            artworkJob.cancel()
            subtitleJob.cancel()
            sessionJob.cancel()
            serverJob.cancel()
            baseUrlJob.cancel()
            manager.removeListener(listener)
        }
    }

    override fun enqueue(id: String, title: String, uri: String): Result<Unit> = runCatching {
        enqueueWithMetadata(id, title, uri, null).getOrThrow()
    }

    override fun enqueueWithMetadata(
        id: String,
        title: String,
        uri: String,
        imageUrl: String?,
        episodeMetadata: DownloadEpisodeMetadata?,
    ): Result<Unit> = enqueueWithMediaMetadata(id, title, uri, imageUrl, episodeMetadata, null)

    override fun enqueueWithMediaMetadata(
        id: String,
        title: String,
        uri: String,
        imageUrl: String?,
        episodeMetadata: DownloadEpisodeMetadata?,
        mediaMetadata: DownloadMediaMetadata?,
    ): Result<Unit> = runCatching {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia não é válida." }
        val userId = currentUserId ?: error("Faça login para baixar esta mídia.")
        val serverId = downloadServerScopeId(mediaMetadata?.serverId ?: currentServerId, currentBaseUrl)
            ?: error("Não foi possível identificar o servidor deste download.")
        val requestId = serverScopedDownloadRequestId(userId, serverId, id)
        val normalizedImageUrl = imageUrl?.takeIf(String::isNotBlank)
        val previousImageUrl = metadata.getString("image:$requestId", null)
        titles[requestId] = title
        metadata.edit()
            .putString("title:$requestId", title)
            .putString("item:$requestId", id)
            .putString("owner:$requestId", userId)
            .putString("server:$requestId", serverId)
            .apply {
                if (normalizedImageUrl == null) remove("image:$requestId") else putString("image:$requestId", normalizedImageUrl)
            }
            .apply()
        if (normalizedImageUrl != previousImageUrl) {
            synchronized(artworkPersistenceLock) { artworkStore.remove(requestId) }
        }
        artworkRequestsStarted.remove(requestId)
        addDownloadThroughService(
            downloadRequestFor(requestId, uri, currentBaseUrl, currentAccessToken, episodeMetadata, mediaMetadata),
        )
        normalizedImageUrl?.let { scheduleArtworkCaching(requestId, it) }
        mediaMetadata?.let { scheduleSubtitleCaching(requestId, id, userId, it) }
    }

    override fun retry(downloadId: String, title: String, uri: String): Result<Unit> = runCatching {
        require(downloadId.isNotBlank()) { "O identificador do download é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia não é válida." }
        val userId = currentUserId ?: error("Faça login para baixar esta mídia.")
        val requestId = requestIdForCurrentUser(downloadId)
            ?: error("Este download não está mais na fila.")
        val request = manager.downloadIndex.getDownload(requestId)?.request
        val requestMetadata = request?.data?.let(::decodeDownloadRequestMetadata) ?: DownloadRequestMetadata()
        val savedServerId = (metadata.getString("server:$requestId", null) ?: requestMetadata.media?.serverId)
            ?.trim()
            ?.takeIf(String::isNotEmpty)
        val currentServerScope = downloadServerScopeId(currentServerId, currentBaseUrl)
        if (!canRetryDownloadOnServer(savedServerId, currentServerScope)) {
            if (savedServerId == null) {
                error("Este download antigo não registra o servidor de origem. Remova-o e baixe novamente no servidor correto.")
            }
            error("Conecte-se ao servidor original para tentar novamente este download.")
        }
        val itemId = metadata.getString("item:$requestId", null) ?: publicDownloadItemId(requestId, userId)
        titles[requestId] = title
        metadata.edit()
            .putString("title:$requestId", title)
            .putString("item:$requestId", itemId)
            .putString("owner:$requestId", userId)
            .putString("server:$requestId", savedServerId)
            .apply()
        // Re-adding the same request makes Media3 restart a failed download
        // while preserving its stable id and metadata in the local index.
        //
        // The stored URL is re-pointed at the address in use now. It was captured when
        // the download was queued, and the app switches between the LAN and the public
        // address on its own — retrying the old one fails forever without saying why.
        artworkRequestsStarted.remove(requestId)
        subtitleRequestsStarted.remove(requestId)
        addDownloadThroughService(
            downloadRequestFor(
                requestId, uri, currentBaseUrl, currentAccessToken,
                requestMetadata.episode, requestMetadata.media,
            ),
        )
        metadata.getString("image:$requestId", null)?.let { scheduleArtworkCaching(requestId, it) }
        requestMetadata.media?.let { scheduleSubtitleCaching(requestId, itemId, userId, it) }
    }

    override fun remove(downloadId: String): Result<Unit> = runCatching {
        val requestId = requestIdForCurrentUser(downloadId) ?: return@runCatching
        manager.downloadIndex.getDownload(requestId)?.request?.let(::cleanupOfflineSubtitles)
        removeDownloadThroughService(requestId)
        titles.remove(requestId)
        artworkRequestsStarted.remove(requestId)
        cancelSubtitleFetch(requestId)
        synchronized(artworkPersistenceLock) {
            artworkStore.remove(requestId)
            metadata.edit()
                .remove("title:$requestId")
                .remove("item:$requestId")
                .remove("owner:$requestId")
                .remove("server:$requestId")
                .remove("image:$requestId")
                .remove(downloadFailureMetadataKey(requestId))
                .apply()
        }
    }

    override fun removeCompleted(): Result<Unit> = runCatching {
        removeByState(Download.STATE_COMPLETED)
    }

    override fun removeFailed(): Result<Unit> = runCatching {
        removeByState(Download.STATE_FAILED)
    }

    private fun removeByState(targetState: Int) {
        val downloads = manager.downloadIndex.getDownloads().let { cursor ->
            try {
                buildList {
                    while (cursor.moveToNext()) {
                        cursor.download
                            .takeIf { it.state == targetState && belongsToCurrentUser(it.request.id) }
                            ?.let(::add)
                    }
                }
            } finally {
                cursor.close()
            }
        }
        if (downloads.isNotEmpty()) removeDownloadsThroughService(downloads.map { it.request.id })
        downloads.forEach { download ->
            val id = download.request.id
            cleanupOfflineSubtitles(download.request)
            titles.remove(id)
            artworkRequestsStarted.remove(id)
            cancelSubtitleFetch(id)
            synchronized(artworkPersistenceLock) {
                artworkStore.remove(id)
                metadata.edit()
                    .remove("title:$id")
                    .remove("item:$id")
                    .remove("owner:$id")
                    .remove("server:$id")
                    .remove("image:$id")
                    .remove(downloadFailureMetadataKey(id))
                    .apply()
            }
        }
    }

    private fun scheduleArtworkCaching(requestId: String, imageUrl: String) {
        if (artworkStore.uriFor(requestId) != null || !artworkRequestsStarted.add(requestId)) return
        repositoryScope.launch {
            try {
                val baseUrl = currentBaseUrl.ifBlank { sessionRepository.getBaseUrl().first() }
                val accessToken = currentAccessToken ?: sessionRepository.getAccessToken().first()
                val requestUrl = resolveMediaUrl(baseUrl, imageUrl, accessToken) ?: return@launch
                val request = Request.Builder().url(requestUrl).get().build()
                artworkClient.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) return@use
                    val body = response.body ?: return@use
                    val mediaType = body.contentType()?.let { "${it.type}/${it.subtype}" }
                    val extension = artworkExtensionForContentType(mediaType) ?: return@use
                    synchronized(artworkPersistenceLock) {
                        if (metadata.getString("image:$requestId", null) != imageUrl) return@synchronized
                        if (artworkStore.uriFor(requestId) != null) return@synchronized
                        artworkStore.store(requestId, body.byteStream(), extension)
                        artworkUpdates.tryEmit(Unit)
                    }
                }
            } catch (failure: Exception) {
                // Artwork is an optional enhancement; retain the remote URL fallback.
                // Cancellation still belongs to the repository scope, not this request.
                if (failure is kotlinx.coroutines.CancellationException) throw failure
            }
        }
    }

    /** Fetches only the server's authenticated fallback route; delivery URLs are never persisted or requested. */
    private fun scheduleSubtitleCaching(
        requestId: String,
        itemId: String,
        userId: String,
        media: DownloadMediaMetadata,
        sessionSnapshot: FeedbackRequestSession? = null,
        enqueuePersistentRecovery: Boolean = true,
        jobScope: CoroutineScope = repositoryScope,
    ): Job? {
        val job = synchronized(subtitleJobsLock) {
            if (media.subtitles.isEmpty()) return null
            if (!subtitleRequestsStarted.add(requestId)) return subtitleJobs[requestId]
            jobScope.launch(start = CoroutineStart.LAZY) {
                subtitleRetryableFailures.remove(requestId)
                try {
                    val fetchSession = sessionSnapshot ?: sessionRepository.getFeedbackRequestSession().first()
                        ?: return@launch
                    val serverScope = downloadServerScopeId(fetchSession.serverId, fetchSession.serverUrl)
                        ?: return@launch
                    if (serverScope != media.serverId || fetchSession.userId != userId) return@launch
                    subtitleSessions[requestId] = fetchSession
                    val jobContext = currentCoroutineContext()
                    for (subtitle in media.subtitles) {
                        jobContext.ensureActive()
                        if (!isCurrentSubtitleSession(fetchSession, userId, serverScope)) return@launch
                        if (subtitleStore.uriFor(serverScope, userId, requestId, subtitle) != null) continue
                        val path = subtitleStreamPath(itemId, subtitle.streamIndex, media.mediaSourceId) ?: continue
                        val url = resolveMediaUrl(fetchSession.serverUrl, path, fetchSession.accessToken) ?: continue
                        val request = authenticatedSubtitleRequest(url, fetchSession)
                        for (attempt in 1..MAX_OFFLINE_SUBTITLE_ATTEMPTS) {
                            jobContext.ensureActive()
                            if (!isCurrentSubtitleSession(fetchSession, userId, serverScope)) return@launch
                            val call = subtitleClient.newCall(request)
                            val activeCalls = subtitleCalls.computeIfAbsent(requestId) { ConcurrentHashMap.newKeySet() }
                            activeCalls.add(call)
                            var retry = false
                            try {
                                when (val transfer = transferOfflineSubtitle(
                                    call = call,
                                    isSessionCurrent = {
                                        isCurrentSubtitleSession(fetchSession, userId, serverScope)
                                    },
                                    persistBody = { body ->
                                        synchronized(subtitlePersistenceLock) {
                                            jobContext.ensureActive()
                                            if (!isInDownloadIndex(requestId)) {
                                                null
                                            } else {
                                                subtitleStore.store(
                                                    serverScope,
                                                    userId,
                                                    requestId,
                                                    subtitle,
                                                    body,
                                                )
                                            }
                                        }
                                    },
                                )) {
                                    is OfflineSubtitleTransferResult.HttpFailure -> {
                                        retry = shouldRetryOfflineSubtitleHttpStatus(transfer.statusCode)
                                    }
                                    is OfflineSubtitleTransferResult.Stored -> subtitleUpdates.tryEmit(Unit)
                                    OfflineSubtitleTransferResult.SessionChanged,
                                    OfflineSubtitleTransferResult.MissingBody,
                                    OfflineSubtitleTransferResult.NotStored -> Unit
                                }
                            } catch (failure: IOException) {
                                retry = isTransientOfflineSubtitleNetworkFailure(failure)
                            } finally {
                                activeCalls.remove(call)
                                if (activeCalls.isEmpty()) subtitleCalls.remove(requestId, activeCalls)
                            }
                            if (!retry) break
                            if (attempt == MAX_OFFLINE_SUBTITLE_ATTEMPTS) {
                                subtitleRetryableFailures.add(requestId)
                                if (enqueuePersistentRecovery) subtitleRecoveryWorkScheduler.enqueueAfterTransientFailure()
                                break
                            }
                            delay(offlineSubtitleRetryDelayMillis(attempt))
                        }
                    }
                } catch (failure: Exception) {
                    if (failure is kotlinx.coroutines.CancellationException) throw failure
                    // Sidecars are optional. Their failures must not change the video download state.
                } finally {
                    subtitleUpdates.tryEmit(Unit)
                }
            }.also { createdJob ->
                subtitleJobs[requestId] = createdJob
                createdJob.invokeOnCompletion {
                    synchronized(subtitleJobsLock) {
                        subtitleCalls.remove(requestId)?.forEach(Call::cancel)
                        subtitleSessions.remove(requestId)
                        subtitleJobs.remove(requestId, createdJob)
                        subtitleRequestsStarted.remove(requestId)
                    }
                }
            }
        }
        job.start()
        return job
    }

    private suspend fun isCurrentSubtitleSession(
        expectedSession: FeedbackRequestSession,
        userId: String,
        serverScope: String,
    ): Boolean {
        val activeSession = sessionRepository.getFeedbackRequestSession().first() ?: return false
        return activeSession.userId == userId &&
            activeSession.userId == expectedSession.userId &&
            downloadServerScopeId(activeSession.serverId, activeSession.serverUrl) == serverScope
    }

    private fun pendingSubtitleRecoveryCandidates(
        session: FeedbackRequestSession,
    ): List<OfflineSubtitleRecoveryCandidate> {
        val serverScope = downloadServerScopeId(session.serverId, session.serverUrl) ?: return emptyList()
        val candidates = mutableListOf<OfflineSubtitleRecoveryCandidate>()
        val cursor = manager.downloadIndex.getDownloads()
        try {
            while (cursor.moveToNext()) {
                val requestId = cursor.download.request.id
                val itemId = metadata.getString("item:$requestId", null)?.takeIf(String::isNotBlank) ?: continue
                val requestMetadata = decodeDownloadRequestMetadata(cursor.download.request.data) ?: continue
                val media = requestMetadata.media ?: continue
                candidates += OfflineSubtitleRecoveryCandidate(
                    requestId = requestId,
                    itemId = itemId,
                    ownerUserId = metadata.getString("owner:$requestId", null),
                    media = media,
                )
            }
        } finally {
            cursor.close()
        }

        return selectOfflineSubtitleRecoveries(candidates, session.userId, serverScope) { candidate, subtitle ->
            subtitleStore.uriFor(serverScope, session.userId, candidate.requestId, subtitle) != null
        }
    }

    internal suspend fun recoverPendingSubtitleCachingForWork(workerScope: CoroutineScope): Boolean {
        val session = sessionRepository.getFeedbackRequestSession().first() ?: return true
        val candidates = pendingSubtitleRecoveryCandidates(session)
        candidates.mapNotNull { candidate -> cancelSubtitleFetch(candidate.requestId) }.joinAll()
        val jobs = candidates.mapNotNull { candidate ->
            scheduleSubtitleCaching(
                requestId = candidate.requestId,
                itemId = candidate.itemId,
                userId = session.userId,
                media = candidate.media,
                sessionSnapshot = session,
                enqueuePersistentRecovery = false,
                jobScope = workerScope,
            )?.let { job -> candidate.requestId to job }
        }
        try {
            jobs.map { it.second }.joinAll()
        } catch (cancelled: kotlinx.coroutines.CancellationException) {
            jobs.forEach { (requestId, _) -> cancelSubtitleFetch(requestId) }
            throw cancelled
        }
        val activeSession = sessionRepository.getFeedbackRequestSession().first() ?: return true
        val hasTransientFailures = jobs.any { (requestId, _) -> requestId in subtitleRetryableFailures }
        return !shouldRetryOfflineSubtitleRecoveryWork(
            sessionChanged = activeSession != session,
            hasTransientFailures = hasTransientFailures,
        )
    }

    private fun cancelSubtitleFetch(requestId: String): Job? = synchronized(subtitleJobsLock) {
        val job = subtitleJobs[requestId]
        subtitleCalls.remove(requestId)?.forEach(Call::cancel)
        job?.cancel()
        job
    }

    private fun cleanupOfflineSubtitles(request: DownloadRequest) {
        cancelSubtitleFetch(request.id)
        val userId = metadata.getString("owner:${request.id}", null) ?: return
        val media = decodeDownloadRequestMetadata(request.data)?.media ?: return
        synchronized(subtitlePersistenceLock) {
            subtitleStore.remove(media.serverId, userId, request.id, media.subtitles)
        }
    }

    private fun subtitleStreamPath(itemId: String, streamIndex: Int, mediaSourceId: String?): String? =
        offlineSubtitleStreamPath(itemId, streamIndex, mediaSourceId)

    /**
     * Every queue mutation goes through [DownloadService].
     *
     * Media3 only drives the queue while the service is running: it is what
     * posts the ongoing notification and what keeps a download alive when the
     * app leaves the foreground. Calling `manager.addDownload` directly left the
     * service dormant, so a queued or partially downloaded item stopped as soon
     * as the process died and the user never saw a notification.
     */
    private fun addDownloadThroughService(request: DownloadRequest) {
        Media3DownloadService.sendAddDownload(
            appContext,
            DownloadService::class.java,
            request,
            false,
        )
    }

    private fun removeDownloadThroughService(requestId: String) {
        Media3DownloadService.sendRemoveDownload(
            appContext,
            DownloadService::class.java,
            requestId,
            false,
        )
    }

    private fun removeDownloadsThroughService(requestIds: List<String>) {
        // Media3 1.11 has no bulk "sendRemoveDownloads", so each id is sent
        // separately; the service is already running after the first one.
        requestIds.forEach(::removeDownloadThroughService)
    }

    private fun resumeDownloadsThroughService() {
        runCatching {
            Media3DownloadService.sendResumeDownloads(
                appContext,
                DownloadService::class.java,
                false,
            )
        }
    }

    private fun pauseDownloadsThroughService() {
        Media3DownloadService.sendPauseDownloads(
            appContext,
            DownloadService::class.java,
            false,
        )
    }

    override fun pauseAll(): Result<Unit> = runCatching {
        pauseDownloadsThroughService()
        metadata.edit().putBoolean(KEY_QUEUE_PAUSED, true).apply()
        queuePaused.value = true
    }

    override fun resumeAll(): Result<Unit> = runCatching {
        Media3DownloadService.sendResumeDownloads(
            appContext,
            DownloadService::class.java,
            false,
        )
        metadata.edit().putBoolean(KEY_QUEUE_PAUSED, false).apply()
        queuePaused.value = false
    }

    private fun snapshot(): List<DownloadEntry> {
        val cursor = manager.downloadIndex.getDownloads()
        return try {
            buildList {
                while (cursor.moveToNext()) {
                    val download = cursor.download
                    val requestId = download.request.id
                    val owner = metadata.getString("owner:$requestId", null)
                    // Entries from releases that predate per-account scoping are
                    // adopted by the signed-in account the first time they are
                    // seen. Without this they stay filtered out forever: never
                    // listed, never removable, never reclaimed by "clear".
                    if (shouldAdoptLegacyDownload(requestId, owner, currentUserId)) {
                        metadata.edit()
                            .putString("owner:$requestId", currentUserId)
                            .putString("item:$requestId", requestId)
                            .apply()
                        // Resolve the id explicitly: `toEntry` reads the keys
                        // written above, and depending on when the SharedPreferences
                        // edit becomes visible would make this entry's id racy.
                        add(download.toEntry(itemIdOverride = requestId))
                    } else if (belongsToCurrentUser(requestId)) {
                        add(download.toEntry())
                    }
                }
            }.sortedBy { it.title.lowercase() }
        } finally { cursor.close() }
    }

    private fun Download.toEntry(itemIdOverride: String? = null): DownloadEntry {
        val imageUrl = metadata.getString("image:${request.id}", null)
        val offlineArtworkUri = artworkStore.uriFor(request.id)
        if (offlineArtworkUri == null) imageUrl?.let { scheduleArtworkCaching(request.id, it) }

        val requestMetadata = decodeDownloadRequestMetadata(request.data)
        val serverId = metadata.getString("server:${request.id}", null)
            ?: requestMetadata?.media?.serverId
        val mediaMetadata = requestMetadata?.media?.takeIf { media ->
            media.serverId == sessionRepositoryServerScope() && metadata.getString("owner:${request.id}", null) == currentUserId
        }
        return DownloadEntry(
            id = itemIdOverride
                ?: metadata.getString("item:${request.id}", null)
                ?: publicDownloadItemId(request.id, currentUserId.orEmpty()),
            title = titles[request.id] ?: metadata.getString("title:${request.id}", request.id).orEmpty(),
            imageUrl = imageUrl,
            offlineArtworkUri = offlineArtworkUri,
            episodeMetadata = requestMetadata?.episode,
            offlineSubtitles = mediaMetadata?.let { media ->
                val owner = metadata.getString("owner:${request.id}", null) ?: return@let emptyList()
                subtitleStore.entries(media.serverId, owner, request.id, media.subtitles)
            }.orEmpty(),
            uri = request.uri.toString(),
            state = when (state) {
                Download.STATE_QUEUED, Download.STATE_RESTARTING -> DownloadState.Queued
                Download.STATE_DOWNLOADING -> DownloadState.Downloading
                Download.STATE_COMPLETED -> DownloadState.Completed
                Download.STATE_REMOVING -> DownloadState.Removing
                else -> DownloadState.Failed
            },
            percent = percentDownloaded.coerceIn(0f, 100f).toInt(),
            error = downloadFailureMessage(
                failureReason,
                insufficientStorage = isDownloadFailureDueToInsufficientStorage(metadata, request.id),
            ),
            bytesDownloaded = getBytesDownloaded().coerceAtLeast(0L),
            contentLength = contentLength.takeIf { it > 0L } ?: 0L,
            downloadId = request.id,
            serverId = serverId,
        )
    }

    private fun requirementsFor(enabled: Boolean): Requirements =
        if (enabled) Requirements(Requirements.NETWORK_UNMETERED) else Requirements(0)

    private fun sessionRepositoryServerScope(): String =
        sessionRepositoryServerIdOrBaseUrl().ifBlank { currentBaseUrl.trimEnd('/') }

    private fun sessionRepositoryServerIdOrBaseUrl(): String =
        currentServerId?.takeIf(String::isNotBlank) ?: currentBaseUrl.trimEnd('/')

    /** The request id an existing entry is stored under, in either of its two shapes. */
    private fun requestIdForCurrentUser(downloadId: String): String? {
        currentUserId ?: error("Faça login para gerenciar downloads.")
        if (!isInDownloadIndex(downloadId) || !belongsToCurrentUser(downloadId)) return null
        return downloadId
    }

    private fun isInDownloadIndex(requestId: String): Boolean =
        manager.downloadIndex.getDownload(requestId) != null

    private fun belongsToCurrentUser(requestId: String): Boolean =
        downloadBelongsToUser(metadata.getString("owner:$requestId", null), currentUserId)

    private companion object {
        const val KEY_QUEUE_PAUSED = "queue_paused"
        const val KEY_WIFI_ONLY = "wifi_only"
    }
}

/**
 * The request handed to the download service, pointed at the address in use now.
 *
 * Both the first enqueue and "Tentar novamente" go through here, and that is the
 * point: a URL is written into the Media3 index and outlives the address it was built
 * with, so a download queued at home kept being retried against the LAN address after
 * the app had switched to the public one, and against the token from that session.
 *
 * It is a function rather than an inline builder so the request can be asserted by a
 * test — the repository itself needs the system's `DownloadManager` and cannot be
 * built outside a running app. There must be exactly one `DownloadRequest.Builder`
 * call site in this file; `DownloadRequestRetargetGuardTest` fails if a second one
 * appears, because a second one is a request built from a stale address.
 *
 * `@UnstableApi` because Media3 marks the offline `DownloadRequest` as opt-in; the
 * class this used to live in carries the same annotation.
 */
@UnstableApi
internal fun downloadRequestFor(
    requestId: String,
    storedUri: String,
    baseUrl: String,
    accessToken: String?,
    episodeMetadata: DownloadEpisodeMetadata? = null,
    mediaMetadata: DownloadMediaMetadata? = null,
): DownloadRequest = DownloadRequest.Builder(
    requestId,
    Uri.parse(cleartextSafeDownloadUrl(storedUri, baseUrl, accessToken)),
).setData(encodeDownloadRequestMetadata(episodeMetadata, mediaMetadata)).build()

internal fun cleartextSafeDownloadUrl(storedUri: String, baseUrl: String, accessToken: String?): String {
    val credentialFreeUrl = retargetMediaUrl(storedUri, baseUrl, accessToken = null)
    val parsedUrl = credentialFreeUrl.toHttpUrlOrNull()
        ?: error("A URL do download não é válida.")
    CleartextTrafficPolicy.requireAllowed(parsedUrl)
    return retargetMediaUrl(storedUri, baseUrl, accessToken)
}

internal fun offlineSubtitleStreamPath(itemId: String, streamIndex: Int, mediaSourceId: String?): String? {
    if (itemId.isBlank() || streamIndex !in 0..100_000) return null
    val itemSegment = encodeSubtitleComponent(itemId)
    val path = "Items/$itemSegment/Subtitles/$streamIndex/Stream"
    val source = mediaSourceId?.takeIf(String::isNotBlank) ?: return path
    return "$path?MediaSourceId=${encodeSubtitleComponent(source)}"
}

private fun encodeSubtitleComponent(value: String): String =
    URLEncoder.encode(value, StandardCharsets.UTF_8.name()).replace("+", "%20")

internal fun offlineSubtitleHttpClient(
    identityInterceptor: Interceptor,
    connectedRouteIdentityInterceptor: Interceptor,
): OkHttpClient =
    OkHttpClient.Builder()
        .addInterceptor(identityInterceptor)
        .enforceLocalNetworkCleartextPolicy()
        .addNetworkInterceptor(connectedRouteIdentityInterceptor)
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .followRedirects(false)
        .followSslRedirects(false)
        .build()

internal fun authenticatedSubtitleRequest(url: String, session: FeedbackRequestSession): Request =
    Request.Builder()
        .url(url)
        .tag(FeedbackRequestSession::class.java, session)
        .get()
        .build()
