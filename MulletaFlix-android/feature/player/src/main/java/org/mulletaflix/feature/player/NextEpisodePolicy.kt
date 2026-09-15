package org.mulletaflix.feature.player

/**
 * Determines whether the Next Episode overlay or prompt should be shown.
 *
 * Shows when:
 * 1. An active next episode countdown is in progress.
 * 2. Playback has completed (or is at the very end within threshold) and a next episode is available.
 */
internal fun shouldShowNextEpisodePrompt(
    hasNextEpisode: Boolean,
    isPlaybackEnded: Boolean,
    countdownActive: Boolean,
): Boolean {
    if (!hasNextEpisode) return false
    return countdownActive || isPlaybackEnded
}

internal fun formatNextEpisodeSubtitle(
    seasonNumber: Int?,
    episodeNumber: Int?,
): String? {
    return when {
        seasonNumber != null && episodeNumber != null -> "T$seasonNumber : E$episodeNumber"
        episodeNumber != null -> "Episódio $episodeNumber"
        else -> null
    }
}
