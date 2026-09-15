package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class GestureSeekPolicyTest {
    @Test
    fun `horizontal drag maps proportionally to playback time`() {
        assertEquals(10_000L, seekDeltaFromHorizontalDrag(100f, 1_000f, 100_000L))
        assertEquals(-10_000L, seekDeltaFromHorizontalDrag(-100f, 1_000f, 100_000L))
    }

    @Test
    fun `invalid viewport or duration produces no displacement`() {
        assertEquals(0L, seekDeltaFromHorizontalDrag(100f, 0f, 100_000L))
        assertEquals(0L, seekDeltaFromHorizontalDrag(100f, 1_000f, 0L))
    }
}
