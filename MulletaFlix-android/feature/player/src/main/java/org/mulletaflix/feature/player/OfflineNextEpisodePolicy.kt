package org.mulletaflix.feature.player

import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

/** Picks the earliest later episode that is already complete on this device. */
internal fun nextCompletedDownloadedEpisode(
    current: DownloadEntry?,
    downloads: List<DownloadEntry>,
): DownloadEntry? {
    val currentMetadata = current?.episodeMetadata ?: return null
    if (current.state != DownloadState.Completed) return null
    val currentServerId = current.serverId?.takeIf(String::isNotBlank) ?: return null

    return downloads.asSequence()
        .filter { candidate -> candidate.downloadId != current.downloadId && candidate.state == DownloadState.Completed }
        .filter { candidate -> candidate.serverId == currentServerId }
        .filter { candidate -> candidate.episodeMetadata?.seriesId == currentMetadata.seriesId }
        .filter { candidate ->
            val metadata = candidate.episodeMetadata ?: return@filter false
            metadata.seasonNumber > currentMetadata.seasonNumber ||
                (metadata.seasonNumber == currentMetadata.seasonNumber &&
                    metadata.episodeNumber > currentMetadata.episodeNumber)
        }
        .minWithOrNull(
            compareBy<DownloadEntry> { it.episodeMetadata?.seasonNumber }
                .thenBy { it.episodeMetadata?.episodeNumber }
                .thenBy { it.title.lowercase() }
                .thenBy { it.downloadId },
        )
}
