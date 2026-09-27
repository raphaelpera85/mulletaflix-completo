package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow

data class DownloadEntry(
    val id: String,
    val title: String,
    val uri: String,
    val state: DownloadState,
    val percent: Int,
    val error: String? = null,
    val bytesDownloaded: Long = 0L,
    val contentLength: Long = 0L,
    /** Relative server image path saved with the download, when available. */
    val imageUrl: String? = null,
    /** Private local artwork copied for offline display, when available. */
    val offlineArtworkUri: String? = null,
    val episodeMetadata: DownloadEpisodeMetadata? = null,
    val offlineSubtitles: List<OfflineSubtitleEntry> = emptyList(),
)

data class DownloadEpisodeMetadata(
    val seriesId: String,
    val seasonNumber: Int,
    val episodeNumber: Int,
    val seriesName: String? = null,
)

/** Non-secret description of a supported external subtitle returned for a media source. */
data class DownloadSubtitleMetadata(
    val streamIndex: Int,
    val mimeType: String,
    val language: String? = null,
    val label: String? = null,
    val isDefault: Boolean = false,
    val isForced: Boolean = false,
)

/** Metadata needed to fetch and scope optional sidecars for a queued media item. */
data class DownloadMediaMetadata(
    val serverId: String,
    val mediaSourceId: String?,
    val subtitles: List<DownloadSubtitleMetadata>,
)

/** A fully downloaded sidecar, visible only beside its owning media download. */
data class OfflineSubtitleEntry(
    val streamIndex: Int,
    val uri: String,
    val mimeType: String,
    val language: String? = null,
    val label: String? = null,
    val isDefault: Boolean = false,
    val isForced: Boolean = false,
)

enum class DownloadState { Queued, Downloading, Completed, Failed, Removing }

interface DownloadRepository {
    fun observeDownloads(): Flow<List<DownloadEntry>>
    fun observeQueuePaused(): Flow<Boolean> = kotlinx.coroutines.flow.flowOf(false)
    fun observeWifiOnly(): Flow<Boolean> = kotlinx.coroutines.flow.flowOf(false)
    fun setWifiOnly(enabled: Boolean): Result<Unit> = Result.success(Unit)
    fun enqueue(id: String, title: String, uri: String): Result<Unit>
    fun enqueueWithMetadata(
        id: String,
        title: String,
        uri: String,
        imageUrl: String?,
        episodeMetadata: DownloadEpisodeMetadata? = null,
    ): Result<Unit> =
        enqueue(id, title, uri)
    fun enqueueWithMediaMetadata(
        id: String,
        title: String,
        uri: String,
        imageUrl: String?,
        episodeMetadata: DownloadEpisodeMetadata?,
        mediaMetadata: DownloadMediaMetadata?,
    ): Result<Unit> = enqueueWithMetadata(id, title, uri, imageUrl, episodeMetadata)
    fun retry(id: String, title: String, uri: String): Result<Unit>
    fun remove(id: String): Result<Unit>
    /** Removes only completed downloads, preserving queued, active, and failed items. */
    fun removeCompleted(): Result<Unit> = Result.success(Unit)
    /** Removes only failed downloads, preserving queued, active, and completed items. */
    fun removeFailed(): Result<Unit> = Result.success(Unit)
    fun pauseAll(): Result<Unit>
    fun resumeAll(): Result<Unit>
}
