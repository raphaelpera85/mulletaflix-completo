package org.mulletaflix.feature.downloads

import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

internal fun failedDownloads(downloads: List<DownloadEntry>): List<DownloadEntry> =
    downloads
        .asSequence()
        .filter { it.state == DownloadState.Failed }
        .distinctBy { it.id }
        .toList()

internal fun completedDownloads(downloads: List<DownloadEntry>): List<DownloadEntry> =
    downloads.filter { it.state == DownloadState.Completed }
