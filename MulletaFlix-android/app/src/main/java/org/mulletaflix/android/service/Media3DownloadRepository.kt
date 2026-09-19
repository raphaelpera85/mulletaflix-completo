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
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.callbackFlow
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.DownloadState
import java.util.concurrent.ConcurrentHashMap
import javax.inject.Inject
import javax.inject.Singleton

@UnstableApi
@Singleton
class Media3DownloadRepository @Inject constructor(@ApplicationContext context: Context) : DownloadRepository {
    private val manager = DownloadManagerSingleton.get(context)
    private val metadata = context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
    private val titles = ConcurrentHashMap<String, String>()
    private val wifiOnly = MutableStateFlow(metadata.getBoolean(KEY_WIFI_ONLY, false))

    init {
        manager.requirements = requirementsFor(wifiOnly.value)
    }

    override fun observeWifiOnly(): Flow<Boolean> = wifiOnly

    override fun setWifiOnly(enabled: Boolean): Result<Unit> = runCatching {
        manager.requirements = requirementsFor(enabled)
        metadata.edit().putBoolean(KEY_WIFI_ONLY, enabled).apply()
        wifiOnly.value = enabled
    }

    override fun observeDownloads(): Flow<List<DownloadEntry>> = callbackFlow {
        fun emitSnapshot() { trySend(snapshot()) }
        val listener = object : DownloadManager.Listener {
            override fun onDownloadChanged(downloadManager: DownloadManager, download: Download, finalException: Exception?) = emitSnapshot()
            override fun onDownloadRemoved(downloadManager: DownloadManager, download: Download) = emitSnapshot()
        }
        manager.addListener(listener)
        emitSnapshot()
        awaitClose { manager.removeListener(listener) }
    }

    override fun enqueue(id: String, title: String, uri: String): Result<Unit> = runCatching {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia não é válida." }
        titles[id] = title
        metadata.edit().putString("title:$id", title).apply()
        manager.addDownload(DownloadRequest.Builder(id, Uri.parse(uri)).build())
    }

    override fun retry(id: String, title: String, uri: String): Result<Unit> = runCatching {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia não é válida." }
        titles[id] = title
        metadata.edit().putString("title:$id", title).apply()
        // Re-adding the same request makes Media3 restart a failed download
        // while preserving its stable id and metadata in the local index.
        manager.addDownload(DownloadRequest.Builder(id, Uri.parse(uri)).build())
    }

    override fun remove(id: String): Result<Unit> = runCatching {
        manager.removeDownload(id)
        titles.remove(id)
        metadata.edit().remove("title:$id").apply()
    }

    override fun pauseAll(): Result<Unit> = runCatching {
        manager.pauseDownloads()
    }

    override fun resumeAll(): Result<Unit> = runCatching {
        manager.resumeDownloads()
    }

    private fun snapshot(): List<DownloadEntry> {
        val cursor = manager.downloadIndex.getDownloads()
        return try {
            buildList {
                while (cursor.moveToNext()) add(cursor.download.toEntry())
            }.sortedBy { it.title.lowercase() }
        } finally { cursor.close() }
    }

    private fun Download.toEntry() = DownloadEntry(
        id = request.id,
        title = titles[request.id] ?: metadata.getString("title:${request.id}", request.id).orEmpty(),
        uri = request.uri.toString(),
        state = when (state) {
            Download.STATE_QUEUED, Download.STATE_RESTARTING -> DownloadState.Queued
            Download.STATE_DOWNLOADING -> DownloadState.Downloading
            Download.STATE_COMPLETED -> DownloadState.Completed
            Download.STATE_REMOVING -> DownloadState.Removing
            else -> DownloadState.Failed
        },
        percent = percentDownloaded.coerceIn(0f, 100f).toInt(),
        error = failureReason.takeIf { it != Download.FAILURE_REASON_NONE }?.toString(),
        bytesDownloaded = getBytesDownloaded().coerceAtLeast(0L),
        contentLength = contentLength.takeIf { it > 0L } ?: 0L,
    )

    private fun requirementsFor(enabled: Boolean): Requirements =
        if (enabled) Requirements(Requirements.NETWORK_UNMETERED) else Requirements(0)

    private companion object {
        const val KEY_WIFI_ONLY = "wifi_only"
    }
}
