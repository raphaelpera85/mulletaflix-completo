package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
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
}
