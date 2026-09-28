package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DiscoveryProbePolicyTest {
    @Test
    fun `bounds discovery windows to protect battery and wifi lock`() {
        assertEquals(0, boundedDiscoveryTimeoutMs(-1))
        assertEquals(2_500, boundedDiscoveryTimeoutMs(2_500))
        assertEquals(10_000, boundedDiscoveryTimeoutMs(60_000))
    }

    @Test
    fun `uses injected monotonic time for loop and socket deadline`() {
        var elapsedRealtimeMs = 10_000L
        val window = DiscoveryWindow(timeoutMs = 2_500) { elapsedRealtimeMs }

        assertTrue(window.isOpen())
        assertEquals(2_500, window.remainingSocketTimeoutMs())

        elapsedRealtimeMs += 1_750
        assertTrue(window.isOpen())
        assertEquals(750, window.remainingSocketTimeoutMs())

        elapsedRealtimeMs += 750
        assertFalse(window.isOpen())
        assertEquals(1, window.remainingSocketTimeoutMs())
    }

    @Test
    fun `empty monotonic window does not enter loop and keeps socket wait valid`() {
        val window = DiscoveryWindow(timeoutMs = 0) { 0L }

        assertFalse(window.isOpen())
        assertEquals(1, window.remainingSocketTimeoutMs())
    }

    @Test
    fun `schedules repeated probes inside discovery window`() {
        assertEquals(
            listOf(0, 750, 1500, 2250),
            discoveryProbeDelays(timeoutMs = 2_500),
        )
    }

    @Test
    fun `does not schedule probes for an empty window`() {
        assertEquals(emptyList<Int>(), discoveryProbeDelays(timeoutMs = 0))
    }

    @Test
    fun `uses a safe interval when caller provides a non-positive value`() {
        assertEquals(listOf(0, 1, 2), discoveryProbeDelays(timeoutMs = 3, retryIntervalMs = 0))
    }

    @Test
    fun `keeps retry schedule bounded for a short multi-interface scan`() {
        assertEquals(listOf(0, 500, 1000), discoveryProbeDelays(timeoutMs = 1_250, retryIntervalMs = 500))
    }
}
