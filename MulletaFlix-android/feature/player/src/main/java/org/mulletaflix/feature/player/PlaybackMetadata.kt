package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

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
