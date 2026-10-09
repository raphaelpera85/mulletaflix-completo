package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Test

class SyncPlayReconnectPolicyTest {
    @Test
    fun `uses bounded exponential backoff`() {
        assertEquals(1_000L, syncPlayReconnectDelayCeilingMs(0))
        assertEquals(2_000L, syncPlayReconnectDelayCeilingMs(1))
        assertEquals(4_000L, syncPlayReconnectDelayCeilingMs(2))
        assertEquals(8_000L, syncPlayReconnectDelayCeilingMs(3))
        assertEquals(8_000L, syncPlayReconnectDelayCeilingMs(20))
    }

    @Test
    fun `negative attempt uses the initial delay`() {
        assertEquals(1_000L, syncPlayReconnectDelayCeilingMs(-1))
        val delayMs = syncPlayReconnectDelayMs(-1)
        assertTrue(delayMs >= 500L)
        assertTrue(delayMs < 1_000L)
    }

    @Test
    fun `reconnect delay uses bounded jitter below each exponential ceiling`() {
        val ceilings = listOf(1_000L, 2_000L, 4_000L, 8_000L, 8_000L)

        ceilings.forEachIndexed { attempt, ceiling ->
            repeat(100) {
                val delayMs = syncPlayReconnectDelayMs(attempt)
                assertTrue("delay must stay above half of $ceiling ms", delayMs >= ceiling / 2)
                assertTrue("delay must be below $ceiling ms", delayMs < ceiling)
            }
        }
    }

    @Test
    fun `ignores callbacks from superseded sockets`() {
        val activeSocket = Any()
        val oldSocket = Any()

        assertEquals(true, isCurrentSyncPlaySocket(activeSocket, activeSocket))
        assertEquals(false, isCurrentSyncPlaySocket(activeSocket, oldSocket))
        assertEquals(false, isCurrentSyncPlaySocket(null, oldSocket))
    }

    @Test
    fun `rejects callbacks from a stale connection generation`() {
        val socket = Any()

        assertEquals(true, isCurrentSyncPlayConnection(4, 4, "group", socket, socket))
        assertEquals(false, isCurrentSyncPlayConnection(5, 4, "group", socket, socket))
        assertEquals(false, isCurrentSyncPlayConnection(4, 4, null, socket, socket))
    }

    @Test
    fun `late observer receives current connected snapshot`() = runBlocking {
        val tracker = SyncPlayRealtimeConnectionTracker()
        tracker.onConnected()

        assertEquals(SyncPlayConnectionSnapshot(generation = 1, connected = true), tracker.state.first())
    }

    @Test
    fun `reconnect advances generation while connected again`() = runBlocking {
        val tracker = SyncPlayRealtimeConnectionTracker()
        tracker.onConnected()
        tracker.onDisconnected()
        tracker.onConnected()

        assertEquals(SyncPlayConnectionSnapshot(generation = 3, connected = true), tracker.state.first())
    }
}
