package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Test

class SeekJumpIntervalTest {
    @Test
    fun `supports configured intervals and defaults to ten seconds`() {
        assertEquals(listOf(5, 10, 15, 30), SEEK_JUMP_SECONDS_CHOICES)
        assertEquals(10, DEFAULT_SEEK_JUMP_SECONDS)
        SEEK_JUMP_SECONDS_CHOICES.forEach { assertEquals(it, normalizeSeekJumpSeconds(it)) }
    }

    @Test
    fun `unknown stored intervals fall back to ten seconds`() {
        listOf(Int.MIN_VALUE, -1, 0, 1, 11, 60, Int.MAX_VALUE).forEach { invalid ->
            assertEquals(10, normalizeSeekJumpSeconds(invalid))
        }
    }

    @Test
    fun `seek jump delta preserves interval and direction`() {
        SEEK_JUMP_SECONDS_CHOICES.forEach { seconds ->
            assertEquals(seconds * 1_000L, seekJumpDeltaMillis(seconds, forward = true))
            assertEquals(-seconds * 1_000L, seekJumpDeltaMillis(seconds, forward = false))
        }
        assertEquals(10_000L, seekJumpDeltaMillis(11, forward = true))
    }
}
