package org.mulletaflix.feature.player

import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class LatestSyncPlayCommandSchedulerTest {

    @Test
    fun `newer command cancels older scheduled command`() = runTest {
        val scheduler = LatestSyncPlayCommandScheduler(this)
        val applied = mutableListOf<String>()

        scheduler.schedule(delayMs = 1_000L) { applied += "pause" }
        advanceTimeBy(500L)
        scheduler.schedule(delayMs = 500L) { applied += "unpause" }
        advanceTimeBy(500L)
        runCurrent()

        assertEquals(listOf("unpause"), applied)
    }

    @Test
    fun `queue update can invalidate a pending command`() = runTest {
        val scheduler = LatestSyncPlayCommandScheduler(this)
        var applied = false

        scheduler.schedule(delayMs = 1_000L) { applied = true }
        scheduler.cancelPending()
        advanceTimeBy(1_000L)
        runCurrent()

        assertEquals(false, applied)
    }
}
