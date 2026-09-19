package org.mulletaflix.feature.player

/** Maps the local player state to the server's paused flag. */
internal fun isPlaybackPausedForReport(isPlaying: Boolean): Boolean = !isPlaying
