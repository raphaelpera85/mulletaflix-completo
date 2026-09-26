package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Test

class SyncPlayReconnectPolicyTest {
    @Test
    fun `uses bounded exponential backoff`() {
        assertEquals(1_000L, syncPlayReconnectDelayMs(0))
        assertEquals(2_000L, syncPlayReconnectDelayMs(1))
        assertEquals(4_000L, syncPlayReconnectDelayMs(2))
        assertEquals(8_000L, syncPlayReconnectDelayMs(3))
        assertEquals(8_000L, syncPlayReconnectDelayMs(20))
    }

    @Test
    fun `negative attempt uses the initial delay`() {
        assertEquals(1_000L, syncPlayReconnectDelayMs(-1))
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
