package org.mulletaflix.feature.syncplay

/** Adds a seek delta without overflow and clamps it to a known media duration. */
internal fun remoteSeekTargetTicks(
    currentPositionTicks: Long,
    runtimeTicks: Long?,
    deltaTicks: Long,
): Long {
    val duration = runtimeTicks?.takeIf { it > 0L }
    val current = currentPositionTicks.coerceAtLeast(0L).let { position ->
        duration?.let(position::coerceAtMost) ?: position
    }
    val requested = when {
        deltaTicks > 0L && current > Long.MAX_VALUE - deltaTicks -> Long.MAX_VALUE
        deltaTicks < 0L && (deltaTicks == Long.MIN_VALUE || current < -deltaTicks) -> 0L
        else -> current + deltaTicks
    }
    return duration?.let(requested::coerceAtMost) ?: requested
}
