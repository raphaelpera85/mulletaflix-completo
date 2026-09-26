package org.mulletaflix.feature.player

internal fun isSeekAvailable(
    isSeekable: Boolean,
    durationMs: Long,
    seekCommandAvailable: Boolean = true,
): Boolean = isSeekable && durationMs > 0L && seekCommandAvailable

internal fun shouldPersistLocalPlaybackPosition(isSeekable: Boolean, durationMs: Long): Boolean =
    isSeekAvailable(isSeekable, durationMs)

internal fun seekPositionFromFraction(fraction: Float, durationMs: Long): Long? {
    if (durationMs <= 0L) return null
    return (fraction.coerceIn(0f, 1f) * durationMs).toLong()
}

internal fun seekPositionByDelta(currentPositionMs: Long, deltaMs: Long, durationMs: Long): Long? {
    if (durationMs <= 0L) return null
    val current = currentPositionMs.coerceIn(0L, durationMs)
    val requested = when {
        deltaMs > 0L && current > Long.MAX_VALUE - deltaMs -> Long.MAX_VALUE
        deltaMs < 0L && (deltaMs == Long.MIN_VALUE || current < -deltaMs) -> 0L
        else -> current + deltaMs
    }
    return requested.coerceIn(0L, durationMs)
}
