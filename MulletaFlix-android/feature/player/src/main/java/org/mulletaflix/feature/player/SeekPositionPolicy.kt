package org.mulletaflix.feature.player

internal fun seekPositionFromFraction(fraction: Float, durationMs: Long): Long {
    if (durationMs <= 0L) return 0L
    return (fraction.coerceIn(0f, 1f) * durationMs).toLong()
}

internal fun seekPositionByDelta(currentPositionMs: Long, deltaMs: Long, durationMs: Long): Long {
    if (durationMs <= 0L) return 0L
    return (currentPositionMs + deltaMs).coerceIn(0L, durationMs)
}
