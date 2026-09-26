package org.mulletaflix.feature.player

/**
 * Determines whether the Next Episode overlay or prompt should be shown.
 *
 * Shows when:
 * 1. An active next episode countdown is in progress.
 * 2. Playback has completed (or is at the very end within threshold) and a next episode is available.
 *
 * [dismissed] wins over both: once the user cancels, playback has still reached
 * the end, so the second condition stays true and the prompt reappeared
 * immediately — the cancel button appeared to do nothing.
 */
internal fun shouldShowNextEpisodePrompt(
    hasNextEpisode: Boolean,
    isPlaybackEnded: Boolean,
    countdownActive: Boolean,
    dismissed: Boolean = false,
): Boolean {
    if (!hasNextEpisode) return false
    if (dismissed) return false
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
