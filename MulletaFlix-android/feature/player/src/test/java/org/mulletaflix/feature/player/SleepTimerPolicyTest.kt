package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SleepTimerPolicyTest {

    @Test
    fun `accepts supported timer values`() {
        assertEquals(15, normalizeSleepTimerMinutes(15))
        assertEquals(180, normalizeSleepTimerMinutes(180))
    }

    @Test
    fun `rejects unsafe timer values`() {
        assertNull(normalizeSleepTimerMinutes(0))
        assertNull(normalizeSleepTimerMinutes(-10))
        assertNull(normalizeSleepTimerMinutes(181))
    }

    @Test
    fun `formats remaining time for the player`() {
        assertEquals("Pausa em 14min 59s", sleepTimerLabel(899_000L))
        assertEquals("Pausa em 9s", sleepTimerLabel(8_100L))
        assertNull(sleepTimerLabel(null))
    }

    @Test
    fun `marks only the active duration option`() {
        assertTrue(isSleepTimerOptionSelected(SleepTimerMode.COUNTDOWN, 30, 30))
        assertFalse(isSleepTimerOptionSelected(SleepTimerMode.COUNTDOWN, 30, 15))
        assertFalse(isSleepTimerOptionSelected(SleepTimerMode.COUNTDOWN, null, 30))
    }

    @Test
    fun `exactly one option is selected for every armed mode`() {
        // "Ao fim da mídia" has no remaining milliseconds, so asking
        // `remainingMs == null` for the "Desativado" row marked two rows at once
        // and the menu could not say which timer was armed.
        val modes = listOf(
            SleepTimerMode.OFF to null,
            SleepTimerMode.AT_MEDIA_END to null,
            SleepTimerMode.COUNTDOWN to 30,
        )
        val options = listOf(15, 30, 45, 60, 90)

        modes.forEach { (mode, minutes) ->
            val selected = buildList {
                if (isSleepTimerOffSelected(mode)) add("Desativado")
                if (isSleepTimerAtMediaEndSelected(mode)) add("Ao fim da mídia")
                options.filter { isSleepTimerOptionSelected(mode, minutes, it) }
                    .forEach { add("$it") }
            }
            assertEquals("mode $mode must select exactly one row", 1, selected.size)
        }
    }

    @Test
    fun `the media end mode is not also reported as disabled`() {
        assertTrue(isSleepTimerAtMediaEndSelected(SleepTimerMode.AT_MEDIA_END))
        assertFalse(isSleepTimerOffSelected(SleepTimerMode.AT_MEDIA_END))
    }

    @Test
    fun `the off mode is not reported as media end`() {
        assertTrue(isSleepTimerOffSelected(SleepTimerMode.OFF))
        assertFalse(isSleepTimerAtMediaEndSelected(SleepTimerMode.OFF))
    }

    @Test
    fun `describes the pause at media end mode`() {
        assertEquals(
            "Pausa ao fim da mídia",
            sleepTimerDisplayLabel(SleepTimerMode.AT_MEDIA_END, null),
        )
        assertNull(sleepTimerDisplayLabel(SleepTimerMode.OFF, null))
    }
}
