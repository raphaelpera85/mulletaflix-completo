package org.mulletaflix.feature.player

import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Calculates the local wall-clock time when playback is expected to complete,
 * taking into account the remaining playback duration and playback speed.
 */
internal fun calculateEstimatedEndTime(
    currentPositionMs: Long,
    durationMs: Long,
    playbackSpeed: Float = 1f,
    nowMs: Long = System.currentTimeMillis(),
    locale: Locale = Locale.getDefault(),
): String? {
    if (durationMs <= 0 || currentPositionMs >= durationMs) return null
    val effectiveSpeed = if (playbackSpeed > 0f) playbackSpeed else 1f
    val remainingMs = ((durationMs - currentPositionMs) / effectiveSpeed).toLong()
    if (remainingMs <= 0) return null

    val endTimestamp = nowMs + remainingMs
    val formatter = SimpleDateFormat("HH:mm", locale)
    return "Termina às ${formatter.format(Date(endTimestamp))}"
}
