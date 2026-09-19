package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class SeekPositionPolicyTest {
    @Test
    fun `converts fraction to bounded playback position`() {
        assertEquals(30_000L, seekPositionFromFraction(0.5f, 60_000L))
        assertEquals(0L, seekPositionFromFraction(-1f, 60_000L))
        assertEquals(60_000L, seekPositionFromFraction(2f, 60_000L))
    }

    @Test
    fun `returns zero when duration is unavailable`() {
        assertEquals(0L, seekPositionFromFraction(0.5f, 0L))
    }

    @Test
    fun `bounds explicit ten second seek controls`() {
        assertEquals(0L, seekPositionByDelta(5_000L, -10_000L, 60_000L))
        assertEquals(15_000L, seekPositionByDelta(5_000L, 10_000L, 60_000L))
        assertEquals(60_000L, seekPositionByDelta(55_000L, 10_000L, 60_000L))
    }
}
