package org.mulletaflix.feature.player

private const val DEFAULT_PLAYBACK_SPEED = 1f
private const val MIN_PLAYBACK_SPEED = 0.5f
private const val MAX_PLAYBACK_SPEED = 2f

/** Keeps player speed preferences finite and inside the range exposed by the player UI. */
internal fun normalizePlaybackSpeed(speed: Float): Float =
    speed.takeIf(Float::isFinite)?.coerceIn(MIN_PLAYBACK_SPEED, MAX_PLAYBACK_SPEED)
        ?: DEFAULT_PLAYBACK_SPEED
