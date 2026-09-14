package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Test

class AuthStateTest {

    @Test
    fun `automatic selection waits for discovery before using public endpoint`() {
        val state = AuthState(isDiscovering = true)

        assertEquals(null, automaticServerCandidate(state, manuallyEdited = false, connectionStarted = false))
    }

    @Test
    fun `automatic selection chooses discovered LAN endpoint`() {
        val lan = ServerInfo("MulletaFlix LAN", "http://192.168.1.20:8096")
        val state = AuthState(
            serverUrl = DEFAULT_MULLETAFLIX_SERVER_URL,
            discoveredServers = listOf(lan),
        )

        assertEquals(lan.url, automaticServerCandidate(state, manuallyEdited = false, connectionStarted = false))
    }

    @Test
    fun `manual editing prevents automatic endpoint replacement`() {
        val state = AuthState(discoveredServers = listOf(ServerInfo("LAN", "http://192.168.1.20:8096")))

        assertEquals(null, automaticServerCandidate(state, manuallyEdited = true, connectionStarted = false))
    }
}
