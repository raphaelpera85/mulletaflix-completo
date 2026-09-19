package org.mulletaflix.feature.player

private const val MIN_SLEEP_TIMER_MINUTES = 1
private const val MAX_SLEEP_TIMER_MINUTES = 180

/** Keeps sleep-timer choices safe for a mobile playback session. */
internal fun normalizeSleepTimerMinutes(minutes: Int): Int? =
    minutes.takeIf { it in MIN_SLEEP_TIMER_MINUTES..MAX_SLEEP_TIMER_MINUTES }

internal fun sleepTimerLabel(remainingMs: Long?): String? {
    val remaining = remainingMs ?: return null
    if (remaining <= 0L) return "Pausando agora"
    val totalSeconds = (remaining + 999L) / 1_000L
    val minutes = totalSeconds / 60L
    val seconds = totalSeconds % 60L
    return if (minutes > 0L) {
        "Pausa em ${minutes}min ${seconds.toString().padStart(2, '0')}s"
    } else {
        "Pausa em ${seconds}s"
    }
}
