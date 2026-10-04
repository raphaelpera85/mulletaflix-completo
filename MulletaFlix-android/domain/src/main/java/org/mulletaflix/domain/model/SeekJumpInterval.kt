package org.mulletaflix.domain.model

/** Default seek jump preserves the player's existing behavior for current users. */
const val DEFAULT_SEEK_JUMP_SECONDS = 10

/** Supported fixed seek intervals, in seconds. */
val SEEK_JUMP_SECONDS_CHOICES: List<Int> = listOf(5, 10, 15, 30)

/** Returns a supported interval, falling back safely when persisted data is unknown. */
fun normalizeSeekJumpSeconds(seconds: Int): Int =
    seconds.takeIf { it in SEEK_JUMP_SECONDS_CHOICES } ?: DEFAULT_SEEK_JUMP_SECONDS

/** Converts the selected interval and direction into the delta used by the player. */
fun seekJumpDeltaMillis(seconds: Int, forward: Boolean): Long =
    normalizeSeekJumpSeconds(seconds).toLong() * 1_000L * (if (forward) 1L else -1L)
