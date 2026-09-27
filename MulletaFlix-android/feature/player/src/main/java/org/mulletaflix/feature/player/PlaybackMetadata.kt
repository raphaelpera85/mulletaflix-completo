package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata

/**
 * Compact, readable title used by Android media controls and Cast targets.
 * Episode context is included because the raw episode name is often not
 * enough to identify what is currently playing outside the app.
 */
internal fun mediaNotificationTitle(item: MediaItem): String {
    if (item.type != MediaItemType.Episode) return item.name

    val seasonEpisode = buildString {
        item.parentIndexNumber?.let { append("S${it.toString().padStart(2, '0')}") }
        item.indexNumber?.let { append("E${it.toString().padStart(2, '0')}") }
    }.takeIf { it.isNotBlank() }

    return listOfNotNull(item.seriesName, seasonEpisode, item.name)
        .distinct()
        .joinToString(" · ")
        .ifBlank { item.name }
}

internal fun offlineMediaNotificationTitle(
    title: String,
    episode: DownloadEpisodeMetadata?,
): String {
    if (episode == null) return title
    return mediaNotificationTitle(
        MediaItem(
            id = episode.seriesId,
            name = title,
            type = MediaItemType.Episode,
            seriesName = episode.seriesName,
            parentIndexNumber = episode.seasonNumber,
            indexNumber = episode.episodeNumber,
        ),
    )
}
