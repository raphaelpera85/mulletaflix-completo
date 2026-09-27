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
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import okhttp3.Call
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.Request
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.media.retargetMediaUrl
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import org.mulletaflix.domain.repository.DownloadMediaMetadata
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import org.mulletaflix.domain.repository.DownloadState
import java.io.File
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
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()
    private val subtitleClient = offlineSubtitleHttpClient(clientIdentityInterceptor)
    private val artworkPersistenceLock = Any()
    private val artworkUpdates = MutableSharedFlow<Unit>(replay = 1, extraBufferCapacity = 1)
    private val artworkRequestsStarted = ConcurrentHashMap.newKeySet<String>()
    private val subtitleUpdates = MutableSharedFlow<Unit>(replay = 1, extraBufferCapacity = 1)
    private val subtitleRequestsStarted = ConcurrentHashMap.newKeySet<String>()
    private val subtitleJobs = ConcurrentHashMap<String, Job>()
    private val subtitleCalls = ConcurrentHashMap<String, MutableSet<Call>>()
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
        if (queuePaused.value) manager.pauseDownloads()
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
        val requestId = scopedDownloadRequestId(userId, id)
        // A stable request id may be reused after switching server. Remove sidecars
        // under the old server scope before replacing the request metadata.
        manager.downloadIndex.getDownload(requestId)?.request?.let(::cleanupOfflineSubtitles)
        val normalizedImageUrl = imageUrl?.takeIf(String::isNotBlank)
        val previousImageUrl = metadata.getString("image:$requestId", null)
        titles[requestId] = title
        metadata.edit()
            .putString("title:$requestId", title)
            .putString("item:$requestId", id)
            .putString("owner:$requestId", userId)
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

    override fun retry(id: String, title: String, uri: String): Result<Unit> = runCatching {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia não é válida." }
        val userId = currentUserId ?: error("Faça login para baixar esta mídia.")
        val requestId = existingDownloadRequestId(userId, id, ::isInDownloadIndex)
            ?: error("Este download não está mais na fila.")
        titles[requestId] = title
        metadata.edit()
            .putString("title:$requestId", title)
            .putString("item:$requestId", id)
            .putString("owner:$requestId", userId)
            .apply()
        // Re-adding the same request makes Media3 restart a failed download
        // while preserving its stable id and metadata in the local index.
        //
        // The stored URL is re-pointed at the address in use now. It was captured when
        // the download was queued, and the app switches between the LAN and the public
        // address on its own — retrying the old one fails forever without saying why.
        val request = manager.downloadIndex.getDownload(requestId)?.request
        val requestMetadata = request?.data?.let(::decodeDownloadRequestMetadata) ?: DownloadRequestMetadata()
        artworkRequestsStarted.remove(requestId)
        subtitleRequestsStarted.remove(requestId)
        addDownloadThroughService(
            downloadRequestFor(
                requestId, uri, currentBaseUrl, currentAccessToken,
                requestMetadata.episode, requestMetadata.media,
            ),
        )
        metadata.getString("image:$requestId", null)?.let { scheduleArtworkCaching(requestId, it) }
        requestMetadata.media?.let { scheduleSubtitleCaching(requestId, id, userId, it) }
    }

    override fun remove(id: String): Result<Unit> = runCatching {
        val requestId = requestIdForCurrentUser(id) ?: return@runCatching
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
                .remove("image:$requestId")
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
                    .remove("image:$id")
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
    ) {
        if (media.subtitles.isEmpty() || !subtitleRequestsStarted.add(requestId)) return
        val job = repositoryScope.launch(start = CoroutineStart.LAZY) {
            try {
                val baseUrl = currentBaseUrl.ifBlank { sessionRepository.getBaseUrl().first() }
                val serverScope = sessionRepository.getServerId().first()?.takeIf(String::isNotBlank)
                    ?: baseUrl.trimEnd('/')
                if (serverScope != media.serverId || currentUserId != userId) return@launch
                val accessToken = currentAccessToken ?: sessionRepository.getAccessToken().first()
                val jobContext = currentCoroutineContext()
                media.subtitles.forEach { subtitle ->
                    jobContext.ensureActive()
                    if (currentUserId != userId || serverScope != media.serverId) return@forEach
                    if (subtitleStore.uriFor(serverScope, userId, requestId, subtitle) != null) return@forEach
                    val path = subtitleStreamPath(itemId, subtitle.streamIndex, media.mediaSourceId) ?: return@forEach
                    val url = resolveMediaUrl(baseUrl, path, accessToken) ?: return@forEach
                    val request = Request.Builder().url(url).get().build()
                    val call = subtitleClient.newCall(request)
                    val activeCalls = subtitleCalls.computeIfAbsent(requestId) { ConcurrentHashMap.newKeySet() }
                    activeCalls.add(call)
                    try {
                        jobContext.ensureActive()
                        call.execute().use { response ->
                            if (!response.isSuccessful) return@use
                            val body = response.body ?: return@use
                            val storedUri = try {
                                synchronized(subtitlePersistenceLock) {
                                    jobContext.ensureActive()
                                    if (currentUserId != userId || !isInDownloadIndex(requestId)) return@synchronized
                                    subtitleStore.store(serverScope, userId, requestId, subtitle, body.byteStream())
                                }
                            } catch (cancelled: kotlinx.coroutines.CancellationException) {
                                throw cancelled
                            } catch (_: Exception) {
                                null
                            }
                            if (storedUri != null) subtitleUpdates.tryEmit(Unit)
                        }
                    } finally {
                        activeCalls.remove(call)
                        if (activeCalls.isEmpty()) subtitleCalls.remove(requestId, activeCalls)
                    }
                }
            } catch (failure: Exception) {
                if (failure is kotlinx.coroutines.CancellationException) throw failure
                // Sidecars are optional. Their failures must not change the video download state.
            } finally {
                subtitleUpdates.tryEmit(Unit)
            }
        }
        subtitleJobs[requestId] = job
        job.invokeOnCompletion {
            subtitleCalls.remove(requestId)?.forEach(Call::cancel)
            subtitleRequestsStarted.remove(requestId)
            subtitleJobs.remove(requestId, job)
        }
        job.start()
    }

    private fun cancelSubtitleFetch(requestId: String) {
        subtitleJobs.remove(requestId)?.cancel()
        subtitleCalls.remove(requestId)?.forEach(Call::cancel)
        subtitleRequestsStarted.remove(requestId)
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

    override fun pauseAll(): Result<Unit> = runCatching {
        manager.pauseDownloads()
        metadata.edit().putBoolean(KEY_QUEUE_PAUSED, true).apply()
        queuePaused.value = true
    }

    override fun resumeAll(): Result<Unit> = runCatching {
        manager.resumeDownloads()
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
        val mediaMetadata = requestMetadata?.media?.takeIf { media ->
            media.serverId == (sessionRepositoryServerScope()) && metadata.getString("owner:${request.id}", null) == currentUserId
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
            error = downloadFailureMessage(failureReason),
            bytesDownloaded = getBytesDownloaded().coerceAtLeast(0L),
            contentLength = contentLength.takeIf { it > 0L } ?: 0L,
        )
    }

    private fun requirementsFor(enabled: Boolean): Requirements =
        if (enabled) Requirements(Requirements.NETWORK_UNMETERED) else Requirements(0)

    private fun sessionRepositoryServerScope(): String =
        sessionRepositoryServerIdOrBaseUrl().ifBlank { currentBaseUrl.trimEnd('/') }

    private fun sessionRepositoryServerIdOrBaseUrl(): String =
        currentServerId?.takeIf(String::isNotBlank) ?: currentBaseUrl.trimEnd('/')

    /** The request id an existing entry is stored under, in either of its two shapes. */
    private fun requestIdForCurrentUser(itemId: String): String? {
        val userId = currentUserId ?: error("Faça login para gerenciar downloads.")
        return existingDownloadRequestId(userId, itemId, ::isInDownloadIndex)
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
    Uri.parse(retargetMediaUrl(storedUri, baseUrl, accessToken)),
).setData(encodeDownloadRequestMetadata(episodeMetadata, mediaMetadata)).build()

internal fun offlineSubtitleStreamPath(itemId: String, streamIndex: Int, mediaSourceId: String?): String? {
    if (itemId.isBlank() || streamIndex !in 0..100_000) return null
    val itemSegment = encodeSubtitleComponent(itemId)
    val path = "Items/$itemSegment/Subtitles/$streamIndex/Stream"
    val source = mediaSourceId?.takeIf(String::isNotBlank) ?: return path
    return "$path?MediaSourceId=${encodeSubtitleComponent(source)}"
}

private fun encodeSubtitleComponent(value: String): String =
    URLEncoder.encode(value, StandardCharsets.UTF_8.name()).replace("+", "%20")

internal fun offlineSubtitleHttpClient(identityInterceptor: Interceptor): OkHttpClient =
    OkHttpClient.Builder()
        .addInterceptor(identityInterceptor)
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .followRedirects(false)
        .followSslRedirects(false)
        .build()
