package org.mulletaflix.feature.downloads

import org.mulletaflix.domain.repository.DownloadEntry

internal data class DownloadStorageSummary(
    val downloadedBytes: Long,
    val knownContentBytes: Long,
    val itemCount: Int,
)

internal fun summarizeDownloadStorage(downloads: List<DownloadEntry>): DownloadStorageSummary =
    DownloadStorageSummary(
        downloadedBytes = downloads.sumOf { it.bytesDownloaded.coerceAtLeast(0L) },
        knownContentBytes = downloads.sumOf { it.contentLength.coerceAtLeast(0L) },
        itemCount = downloads.size,
    )

internal fun formatStorageBytes(bytes: Long): String {
    val value = bytes.coerceAtLeast(0L).toDouble()
    return when {
        value >= 1024 * 1024 * 1024 -> "%.1f GB".format(value / (1024 * 1024 * 1024))
        value >= 1024 * 1024 -> "%.1f MB".format(value / (1024 * 1024))
        value >= 1024 -> "%.1f KB".format(value / 1024)
        else -> "${bytes.coerceAtLeast(0L)} B"
    }
}
