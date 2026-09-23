package org.mulletaflix.feature.player

private const val MIN_SLEEP_TIMER_MINUTES = 1
private const val MAX_SLEEP_TIMER_MINUTES = 180

enum class SleepTimerMode {
    OFF,
    COUNTDOWN,
    AT_MEDIA_END,
}

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

internal fun sleepTimerDisplayLabel(mode: SleepTimerMode, remainingMs: Long?): String? = when (mode) {
    SleepTimerMode.OFF -> null
    SleepTimerMode.AT_MEDIA_END -> "Pausa ao fim da mídia"
    SleepTimerMode.COUNTDOWN -> sleepTimerLabel(remainingMs)
}

/**
 * Whether the "Desativado" row is the armed option.
 *
 * Derived from the single [SleepTimerMode] rather than from `remainingMs == null`.
 * "Ao fim da mídia" also has no remaining milliseconds, so asking that question
 * separately marked *two* rows as chosen at once and the user could not tell
 * which timer was armed.
 */
internal fun isSleepTimerOffSelected(mode: SleepTimerMode): Boolean = mode == SleepTimerMode.OFF

/** Whether the "Ao fim da mídia" row is the armed option. */
internal fun isSleepTimerAtMediaEndSelected(mode: SleepTimerMode): Boolean =
    mode == SleepTimerMode.AT_MEDIA_END

/** Returns whether a duration option is the timer currently shown to the user. */
internal fun isSleepTimerOptionSelected(
    mode: SleepTimerMode,
    selectedMinutes: Int?,
    optionMinutes: Int,
): Boolean = mode == SleepTimerMode.COUNTDOWN && selectedMinutes == optionMinutes

/**
 * The sleep timer as a value, so it can be carried across a whole-state handoff.
 *
 * `loadOffline` replaces the state object instead of copying it, so the three
 * fields cannot simply be left alone the way they are on the streaming path: they
 * have to be handed over explicitly or the timer dies with the previous item.
 */
internal data class SleepTimerSelection(
    val mode: SleepTimerMode = SleepTimerMode.OFF,
    val minutes: Int? = null,
    val remainingMs: Long? = null,
) {
    companion object {
        val Off = SleepTimerSelection()
    }
}

internal fun PlayerState.sleepTimerSelection(): SleepTimerSelection = SleepTimerSelection(
    mode = sleepTimerMode,
    minutes = sleepTimerMinutes,
    remainingMs = sleepTimerRemainingMs,
)

/**
 * The state a **different item** starts from inside the same playback session.
 *
 * Everything that describes the previous item is dropped — its chapters, its
 * "Pular Créditos" target, its next-episode prompt — because leaving any of it in
 * place showed the previous episode's chapter name over the new one and kept a
 * skip button live that seeked the new item to the old item's segment.
 *
 * The three sleep-timer fields are deliberately **absent**. The timer belongs to
 * the session, not to the item: "pausar em 30 minutos" is a promise about the
 * next 30 minutes of watching. Resetting them here meant the promise died at the
 * first item change — and the app changes the item on its own, when the
 * next-episode countdown advances to the following episode. The timer was
 * cancelled and cleared before it could ever fire, on exactly the path where a
 * viewer most needs it: falling asleep to a series. It is cleared when the
 * session changes, which is where it stops meaning anything.
 */
internal fun PlayerState.forNewItem(
    aspectRatio: VideoAspectRatio,
    subtitleColor: String,
): PlayerState = copy(
    title = null,
    isPlaying = false,
    isBuffering = true,
    currentPosition = 0L,
    duration = 0L,
    nextEpisode = null,
    nextEpisodeCountdown = null,
    error = null,
    isNetworkOffline = false,
    aspectRatio = aspectRatio,
    subtitleColor = subtitleColor,
    showSkipIntro = false,
    showSkipCredits = false,
    skipTargetPosition = null,
    currentChapterName = null,
    chapters = emptyList(),
    playbackStats = null,
    nextEpisodePromptDismissed = false,
)

/**
 * The state after the sleep timer's countdown reaches zero.
 *
 * The armed countdown wins over an auto-advance that is already scheduled. The
 * two used to act independently: the timer paused playback, and the
 * next-episode countdown that was running at that moment went on to call
 * `loadMedia(next.id)`, so the "pause in N minutes" the user asked for was
 * immediately undone by the next episode starting. Cancelling the countdown is
 * what makes the pause stick; the prompt is dismissed with it because playback
 * stays stopped and would otherwise keep offering the next episode on screen.
 */
internal fun PlayerState.afterSleepTimerExpiry(): PlayerState = copy(
    sleepTimerRemainingMs = null,
    sleepTimerMinutes = null,
    sleepTimerMode = SleepTimerMode.OFF,
    nextEpisodeCountdown = null,
    nextEpisodePromptDismissed = true,
)
