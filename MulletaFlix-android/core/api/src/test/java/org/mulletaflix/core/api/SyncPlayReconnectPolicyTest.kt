package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
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
}
