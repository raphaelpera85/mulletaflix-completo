package org.mulletaflix.feature.downloads

import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

internal enum class DownloadStatusFilter(val label: String) {
    All("Todos"),
    InProgress("Em andamento"),
    Completed("Concluídos"),
    Failed("Falhos"),
}

/** Filters the offline queue without changing its server/repository ordering. */
internal fun filterDownloads(
    downloads: List<DownloadEntry>,
    query: String,
    statusFilter: DownloadStatusFilter = DownloadStatusFilter.All,
): List<DownloadEntry> {
    val normalizedQuery = query.trim()
    return downloads.filter { entry ->
        val matchesQuery = normalizedQuery.isEmpty() ||
            entry.title.contains(normalizedQuery, ignoreCase = true)
        val matchesStatus = when (statusFilter) {
            DownloadStatusFilter.All -> true
            DownloadStatusFilter.InProgress -> entry.state == DownloadState.Queued ||
                entry.state == DownloadState.Downloading
            DownloadStatusFilter.Completed -> entry.state == DownloadState.Completed
            DownloadStatusFilter.Failed -> entry.state == DownloadState.Failed
        }
        matchesQuery && matchesStatus
    }
}
