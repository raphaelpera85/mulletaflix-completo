package org.mulletaflix.feature.syncplay

import org.junit.Assert.assertEquals
import org.junit.Test

class RemoteSeekPolicyTest {
    @Test
    fun `forward seek stops at known media duration`() {
        assertEquals(
            350_000_000L,
            remoteSeekTargetTicks(
                currentPositionTicks = 100_000_000L,
                runtimeTicks = 350_000_000L,
                deltaTicks = 300_000_000L,
            ),
        )
    }

    @Test
    fun `backward seek stops at beginning`() {
        assertEquals(
            0L,
            remoteSeekTargetTicks(
                currentPositionTicks = 100_000_000L,
                runtimeTicks = 350_000_000L,
                deltaTicks = -300_000_000L,
            ),
        )
    }

    @Test
    fun `backward seek uses media end when reported position is stale`() {
        assertEquals(
            50_000_000L,
            remoteSeekTargetTicks(
                currentPositionTicks = 400_000_000L,
                runtimeTicks = 350_000_000L,
                deltaTicks = -300_000_000L,
            ),
        )
    }

    @Test
    fun `seek keeps current step when media duration is unavailable`() {
        assertEquals(
            400_000_000L,
            remoteSeekTargetTicks(
                currentPositionTicks = 100_000_000L,
                runtimeTicks = null,
                deltaTicks = 300_000_000L,
            ),
        )
    }

    @Test
    fun `forward seek saturates instead of overflowing`() {
        assertEquals(
            Long.MAX_VALUE,
            remoteSeekTargetTicks(
                currentPositionTicks = Long.MAX_VALUE - 10,
                runtimeTicks = null,
                deltaTicks = 20,
            ),
        )
    }
}
