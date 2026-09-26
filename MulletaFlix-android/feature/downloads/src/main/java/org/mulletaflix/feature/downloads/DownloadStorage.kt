package org.mulletaflix.feature.downloads

import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState
import java.util.Locale

private val storageLocale = Locale.forLanguageTag("pt-BR")

internal data class DownloadStorageSummary(
    val downloadedBytes: Long,
    val knownContentBytes: Long,
    val itemCount: Int,
)

internal enum class DownloadStorageOrder(val label: String) {
    LargestFirst("Maior primeiro"),
    SmallestFirst("Menor primeiro"),
}

internal fun summarizeDownloadStorage(downloads: List<DownloadEntry>): DownloadStorageSummary =
    DownloadStorageSummary(
        downloadedBytes = downloads.sumOf { it.bytesDownloaded.coerceAtLeast(0L) },
        knownContentBytes = downloads.sumOf { it.contentLength.coerceAtLeast(0L) },
        itemCount = downloads.size,
    )

internal fun sortDownloadsByStorage(
    downloads: List<DownloadEntry>,
    order: DownloadStorageOrder,
): List<DownloadEntry> {
    val withKnownUsage = downloads.filter(::hasKnownDownloadedSize)
    val withoutKnownUsage = downloads
        .filterNot(::hasKnownDownloadedSize)
        .sortedWith(compareBy<DownloadEntry> { it.title.lowercase() }.thenBy { it.id })
    val sortedKnownUsage = when (order) {
        DownloadStorageOrder.LargestFirst -> withKnownUsage.sortedWith(
            compareByDescending<DownloadEntry> { it.bytesDownloaded }
                .thenBy { it.title.lowercase() }
                .thenBy { it.id },
        )
        DownloadStorageOrder.SmallestFirst -> withKnownUsage.sortedWith(
            compareBy<DownloadEntry> { it.bytesDownloaded }
                .thenBy { it.title.lowercase() }
                .thenBy { it.id },
        )
    }
    return sortedKnownUsage + withoutKnownUsage
}

internal fun downloadStorageLabel(entry: DownloadEntry): String {
    if (!hasKnownDownloadedSize(entry)) return "Tamanho ainda não informado"

    val downloaded = formatStorageBytes(entry.bytesDownloaded)
    val total = entry.contentLength.takeIf { it > 0L }?.let(::formatStorageBytes)
    return if (total == null) {
        "No dispositivo: $downloaded"
    } else {
        "No dispositivo: $downloaded de $total"
    }
}

private fun hasKnownDownloadedSize(entry: DownloadEntry): Boolean =
    entry.bytesDownloaded > 0L || entry.contentLength > 0L || entry.state == DownloadState.Completed

internal fun formatStorageBytes(bytes: Long): String {
    val value = bytes.coerceAtLeast(0L).toDouble()
    return when {
        value >= 1024 * 1024 * 1024 -> String.format(storageLocale, "%.1f GB", value / (1024 * 1024 * 1024))
        value >= 1024 * 1024 -> String.format(storageLocale, "%.1f MB", value / (1024 * 1024))
        value >= 1024 -> String.format(storageLocale, "%.1f KB", value / 1024)
        else -> "${bytes.coerceAtLeast(0L)} B"
    }
}
