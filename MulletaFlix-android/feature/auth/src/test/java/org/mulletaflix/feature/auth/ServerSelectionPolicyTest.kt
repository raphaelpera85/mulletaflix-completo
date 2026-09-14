package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Test

class ServerSelectionPolicyTest {
    @Test
    fun `lan endpoint wins over saved and public fallback`() {
        val lan = ServerInfo("LAN", "http://192.168.1.10:8096")
        val saved = ServerInfo("Saved", "http://mulletaflix.duckdns.org:8096")

        assertEquals(lan.url, preferredServerUrl(listOf(lan), listOf(saved), saved.url))
    }

    @Test
    fun `saved endpoint is used when discovery has no results`() {
        val saved = ServerInfo("Saved", "http://mulletaflix.duckdns.org:8096")

        assertEquals(saved.url, preferredServerUrl(emptyList(), listOf(saved), null))
    }

    @Test
    fun `a discovered endpoint remains eligible when it is also saved`() {
        val lan = ServerInfo("LAN", "http://192.168.1.10:8096")
        val saved = ServerInfo("Saved LAN", lan.url)

        // The startup flow must pass the discovery result to the auto-connect
        // effect even when the same URL exists in the saved-server history.
        assertEquals(lan.url, preferredServerUrl(listOf(lan), listOf(saved), null))
    }

    @Test
    fun `public endpoint is automatically verified after empty discovery`() {
        val state = AuthState(serverUrl = DEFAULT_MULLETAFLIX_SERVER_URL)

        assertEquals(
            DEFAULT_MULLETAFLIX_SERVER_URL,
            automaticServerCandidate(state, manuallyEdited = false, connectionStarted = false),
        )
    }

    @Test
    fun `automatic verification waits while discovery is running or editing`() {
        val state = AuthState(isDiscovering = true)

        assertEquals(null, automaticServerCandidate(state, manuallyEdited = false, connectionStarted = false))
        assertEquals(null, automaticServerCandidate(state.copy(isDiscovering = false), manuallyEdited = true, connectionStarted = false))
        assertEquals(null, automaticServerCandidate(state.copy(isDiscovering = false), manuallyEdited = false, connectionStarted = true))
    }

    @Test
    fun `stale lan discovery falls back to another saved endpoint`() {
        val lan = ServerInfo("LAN", "http://192.168.1.10:8096")
        val remote = ServerInfo("Remote", DEFAULT_MULLETAFLIX_SERVER_URL)

        assertEquals(DEFAULT_MULLETAFLIX_SERVER_URL, fallbackServerCandidate(AuthState(savedServers = listOf(lan, remote)), lan.url))
    }

    @Test
    fun `stale lan discovery falls back to public endpoint when no saved alternative exists`() {
        val lan = ServerInfo("LAN", "http://192.168.1.10:8096")

        assertEquals(DEFAULT_MULLETAFLIX_SERVER_URL, fallbackServerCandidate(AuthState(savedServers = listOf(lan)), lan.url))
    }
}
