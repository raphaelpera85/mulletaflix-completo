package org.mulletaflix.feature.downloads

import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

internal fun completedDownloadIds(entries: List<DownloadEntry>): Set<String> =
    entries.asSequence()
        .filter { it.state == DownloadState.Completed }
        .map { it.id }
        .filter(String::isNotBlank)
        .toSet()

internal fun toggleCompletedDownloadSelection(
    selectedIds: Set<String>,
    id: String,
    completedIds: Set<String>,
): Set<String> {
    if (id !in completedIds) return selectedIds
    return if (id in selectedIds) selectedIds - id else selectedIds + id
}

internal fun reconcileCompletedDownloadSelection(
    selectedIds: Set<String>,
    completedIds: Set<String>,
): Set<String> = selectedIds.intersect(completedIds)

internal fun selectedCompletedDownloads(
    entries: List<DownloadEntry>,
    selectedIds: Set<String>,
): List<DownloadEntry> = entries.filter {
    it.state == DownloadState.Completed && it.id in selectedIds
}
