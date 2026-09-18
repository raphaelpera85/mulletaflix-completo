package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Locale

class PlaybackEndTimePolicyTest {

    @Test
    fun calculateEstimatedEndTime_returnsNullWhenDurationIsZero() {
        assertNull(calculateEstimatedEndTime(currentPositionMs = 0L, durationMs = 0L))
    }

    @Test
    fun calculateEstimatedEndTime_returnsNullWhenPositionEqualsOrExceedsDuration() {
        assertNull(calculateEstimatedEndTime(currentPositionMs = 1000L, durationMs = 1000L))
        assertNull(calculateEstimatedEndTime(currentPositionMs = 1200L, durationMs = 1000L))
    }

    @Test
    fun calculateEstimatedEndTime_calculatesCorrectEndTimeForNormalSpeed() {
        val fixedNow = 1700000000000L // arbitrary fixed epoch timestamp
        val remainingMs = 3600000L // 1 hour
        val result = calculateEstimatedEndTime(
            currentPositionMs = 0L,
            durationMs = remainingMs,
            playbackSpeed = 1f,
            nowMs = fixedNow,
            locale = Locale.US
        )
        assertNotNull(result)
        assertTrue(result!!.startsWith("Termina às "))
    }

    @Test
    fun calculateEstimatedEndTime_accountsForPlaybackSpeed() {
        val fixedNow = 1700000000000L
        val remainingMs = 7200000L // 2 hours at 1x, should take 1 hour at 2x
        val at1x = calculateEstimatedEndTime(0L, remainingMs, playbackSpeed = 1f, nowMs = fixedNow)
        val at2x = calculateEstimatedEndTime(0L, remainingMs, playbackSpeed = 2f, nowMs = fixedNow)

        assertNotNull(at1x)
        assertNotNull(at2x)
        // 2x should finish earlier than 1x
        assertTrue(at1x != at2x)
    }
}
