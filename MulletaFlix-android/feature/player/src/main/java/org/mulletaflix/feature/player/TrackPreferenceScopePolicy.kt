package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.UserMediaPreferenceScope

/** Adds a series override only for episodes with a safe, non-empty series identity. */
internal fun UserMediaPreferenceScope.forPlaybackItem(item: MediaItem): UserMediaPreferenceScope {
    val isEpisode = item.type == MediaItemType.Episode
    return forEpisodeSeries(item.seriesId, isEpisode)
}

internal fun UserMediaPreferenceScope.forEpisodeSeries(seriesId: String?): UserMediaPreferenceScope =
    forEpisodeSeries(seriesId, isEpisode = true)

private fun UserMediaPreferenceScope.forEpisodeSeries(
    seriesId: String?,
    isEpisode: Boolean,
): UserMediaPreferenceScope = copy(
    seriesId = seriesId
        ?.trim()
        ?.takeIf { isEpisode && it.isNotEmpty() && it.length <= 128 },
    isEpisode = isEpisode,
)
