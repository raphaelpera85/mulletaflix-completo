package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class GestureAdjustmentPolicyTest {
    @Test
    fun `brightness remains inside safe screen bounds`() {
        assertEquals(1f, adjustBrightness(0.9f, 0.5f))
        assertEquals(0.01f, adjustBrightness(0.1f, -0.5f))
    }

    @Test
    fun `volume remains inside device bounds`() {
        assertEquals(15, adjustVolume(10, 20, 0.25f))
        assertEquals(0, adjustVolume(2, 20, -1f))
        assertEquals(0, adjustVolume(2, 0, 1f))
    }
}
