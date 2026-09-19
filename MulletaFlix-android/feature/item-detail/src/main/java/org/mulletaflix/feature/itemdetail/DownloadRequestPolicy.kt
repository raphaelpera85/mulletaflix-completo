package org.mulletaflix.feature.itemdetail

import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

/** A failed download may be retried; every other existing entry is already active. */
internal fun hasActiveDownload(entries: List<DownloadEntry>, itemId: String): Boolean =
    entries.any { entry ->
        entry.id == itemId && entry.state != DownloadState.Failed && entry.state != DownloadState.Removing
    }
