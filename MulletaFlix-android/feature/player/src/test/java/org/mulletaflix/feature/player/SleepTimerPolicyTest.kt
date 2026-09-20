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
        assertTrue(isSleepTimerOptionSelected(30, 30))
        assertFalse(isSleepTimerOptionSelected(30, 15))
        assertFalse(isSleepTimerOptionSelected(null, 30))
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
