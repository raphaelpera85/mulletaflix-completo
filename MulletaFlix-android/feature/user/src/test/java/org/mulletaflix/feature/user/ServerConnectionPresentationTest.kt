package org.mulletaflix.feature.user

import org.junit.Assert.assertEquals
import org.junit.Test

class ServerConnectionPresentationTest {

    @Test
    fun `does not invent latency before the server is verified`() {
        assertEquals("Conectado", formatServerConnectionStatus(null))
    }

    @Test
    fun `shows measured non negative latency`() {
        assertEquals("Online • 42 ms de latência", formatServerConnectionStatus(42L))
    }

    @Test
    fun `ignores invalid latency values`() {
        assertEquals("Conectado", formatServerConnectionStatus(-1L))
    }
}
