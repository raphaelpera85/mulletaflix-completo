package org.mulletaflix.android.service

import android.content.Context
import android.net.Uri
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.media3.exoplayer.scheduler.Requirements
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.launch
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.DownloadState
import java.util.concurrent.ConcurrentHashMap
import javax.inject.Inject
import javax.inject.Singleton

@UnstableApi
@Singleton
class Media3DownloadRepository @Inject constructor(
    @ApplicationContext context: Context,
    private val sessionRepository: SessionRepository,
) : DownloadRepository {
    private val manager = DownloadManagerSingleton.get(context)
    private val metadata = context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
    private val titles = ConcurrentHashMap<String, String>()
    private val queuePaused = MutableStateFlow(metadata.getBoolean(KEY_QUEUE_PAUSED, false))
    private val wifiOnly = MutableStateFlow(metadata.getBoolean(KEY_WIFI_ONLY, false))
    private val repositoryScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    @Volatile private var currentUserId: String? = null

    init {
        manager.requirements = requirementsFor(wifiOnly.value)
        if (queuePaused.value) manager.pauseDownloads()
        repositoryScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { currentUserId = it }
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
        val sessionJob = launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect {
                currentUserId = it
                emitSnapshot()
            }
        }
        val listener = object : DownloadManager.Listener {
            override fun onDownloadChanged(downloadManager: DownloadManager, download: Download, finalException: Exception?) = emitSnapshot()
            override fun onDownloadRemoved(downloadManager: DownloadManager, download: Download) = emitSnapshot()
        }
        manager.addListener(listener)
        emitSnapshot()
        awaitClose {
            sessionJob.cancel()
            manager.removeListener(listener)
        }
    }

    override fun enqueue(id: String, title: String, uri: String): Result<Unit> = runCatching {
        enqueueWithMetadata(id, title, uri, null).getOrThrow()
    }

    override fun enqueueWithMetadata(id: String, title: String, uri: String, imageUrl: String?): Result<Unit> = runCatching {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia não é válida." }
        val userId = currentUserId ?: error("Faça login para baixar esta mídia.")
        val requestId = scopedDownloadRequestId(userId, id)
        titles[requestId] = title
        metadata.edit()
            .putString("title:$requestId", title)
            .putString("item:$requestId", id)
            .putString("owner:$requestId", userId)
            .apply {
                if (imageUrl.isNullOrBlank()) remove("image:$requestId") else putString("image:$requestId", imageUrl)
            }
            .apply()
        manager.addDownload(DownloadRequest.Builder(requestId, Uri.parse(uri)).build())
    }

    override fun retry(id: String, title: String, uri: String): Result<Unit> = runCatching {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia não é válida." }
        val userId = currentUserId ?: error("Faça login para baixar esta mídia.")
        val requestId = requestIdFor(userId, id)
        titles[requestId] = title
        metadata.edit()
            .putString("title:$requestId", title)
            .putString("item:$requestId", id)
            .putString("owner:$requestId", userId)
            .apply()
        // Re-adding the same request makes Media3 restart a failed download
        // while preserving its stable id and metadata in the local index.
        manager.addDownload(DownloadRequest.Builder(requestId, Uri.parse(uri)).build())
    }

    override fun remove(id: String): Result<Unit> = runCatching {
        val requestId = requestIdForCurrentUser(id)
        manager.removeDownload(requestId)
        titles.remove(requestId)
        metadata.edit()
            .remove("title:$requestId")
            .remove("item:$requestId")
            .remove("owner:$requestId")
            .remove("image:$requestId")
            .apply()
    }

    override fun removeCompleted(): Result<Unit> = runCatching {
        removeByState(Download.STATE_COMPLETED)
    }

    override fun removeFailed(): Result<Unit> = runCatching {
        removeByState(Download.STATE_FAILED)
    }

    private fun removeByState(targetState: Int) {
        val ids = manager.downloadIndex.getDownloads().let { cursor ->
            try {
                buildList {
                    while (cursor.moveToNext()) {
                        cursor.download
                            .takeIf { it.state == targetState && belongsToCurrentUser(it.request.id) }
                            ?.request
                            ?.id
                            ?.let(::add)
                    }
                }
            } finally {
                cursor.close()
            }
        }
        ids.forEach { id ->
            manager.removeDownload(id)
            titles.remove(id)
            metadata.edit()
                .remove("title:$id")
                .remove("item:$id")
                .remove("owner:$id")
                .remove("image:$id")
                .apply()
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
                    if (isLegacyUnscopedDownload(requestId, owner)) {
                        metadata.edit()
                            .putString("owner:$requestId", currentUserId.orEmpty())
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

    private fun Download.toEntry(itemIdOverride: String? = null) = DownloadEntry(
        id = itemIdOverride
            ?: metadata.getString("item:${request.id}", null)
            ?: publicDownloadItemId(request.id, currentUserId.orEmpty()),
        title = titles[request.id] ?: metadata.getString("title:${request.id}", request.id).orEmpty(),
        imageUrl = metadata.getString("image:${request.id}", null),
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

    private fun requirementsFor(enabled: Boolean): Requirements =
        if (enabled) Requirements(Requirements.NETWORK_UNMETERED) else Requirements(0)

    private fun requestIdFor(userId: String, itemId: String): String = scopedDownloadRequestId(userId, itemId)

    private fun requestIdForCurrentUser(itemId: String): String {
        val userId = currentUserId ?: error("Faça login para gerenciar downloads.")
        val scopedId = requestIdFor(userId, itemId)
        return if (manager.downloadIndex.getDownload(scopedId) != null) scopedId else itemId
    }

    private fun belongsToCurrentUser(requestId: String): Boolean =
        downloadBelongsToUser(metadata.getString("owner:$requestId", null), currentUserId)

    private companion object {
        const val KEY_QUEUE_PAUSED = "queue_paused"
        const val KEY_WIFI_ONLY = "wifi_only"
    }
}
