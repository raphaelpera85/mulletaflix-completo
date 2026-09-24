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
    fun `matching server identity wins over an earlier unrelated discovery`() {
        val unrelated = ServerInfo(
            name = "Other Server",
            url = "http://192.168.1.20:8096",
            serverId = "other-id",
        )
        val matching = ServerInfo(
            name = "MulletaFlix LAN",
            url = "http://192.168.1.10:8096",
            serverId = "saved-id",
        )
        val saved = ServerInfo(
            name = "MulletaFlix Cloud",
            url = DEFAULT_MULLETAFLIX_SERVER_URL,
            serverId = "saved-id",
        )

        assertEquals(
            matching.url,
            preferredServerUrl(listOf(unrelated, matching), listOf(saved), saved.url),
        )
    }

    @Test
    fun `discovery without identity keeps first LAN endpoint for first-time setup`() {
        val first = ServerInfo("First LAN", "http://192.168.1.20:8096")
        val second = ServerInfo("Second LAN", "http://192.168.1.10:8096")

        assertEquals(first.url, preferredServerUrl(listOf(first, second), emptyList(), null))
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
    fun `automatic verification prefers the server it is already logged into`() {
        // A decisão que este arquivo existe para tomar, aplicada também ao endereço
        // que a tela conecta sozinha: numa rede com dois servidores compatíveis, o
        // primeiro a responder não pode tomar o lugar do servidor da conta.
        val unrelated = ServerInfo("Other Server", "http://192.168.1.20:8096", serverId = "other-id")
        val matching = ServerInfo("MulletaFlix LAN", "http://192.168.1.10:8096", serverId = "saved-id")
        val saved = ServerInfo("MulletaFlix Cloud", DEFAULT_MULLETAFLIX_SERVER_URL, serverId = "saved-id")
        val state = AuthState(
            discoveredServers = listOf(unrelated, matching),
            savedServers = listOf(saved),
        )

        assertEquals(
            matching.url,
            automaticServerCandidate(state, manuallyEdited = false, connectionStarted = false),
        )
    }

    @Test
    fun `automatic verification keeps the first LAN server when nothing identifies it`() {
        // Primeira configuração: não há identidade para casar, e o primeiro
        // endereço compatível continua sendo a resposta honesta.
        val first = ServerInfo("First LAN", "http://192.168.1.20:8096")
        val second = ServerInfo("Second LAN", "http://192.168.1.10:8096")
        val state = AuthState(discoveredServers = listOf(first, second))

        assertEquals(
            first.url,
            automaticServerCandidate(state, manuallyEdited = false, connectionStarted = false),
        )
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

    @Test
    fun `any discovered lan endpoint can trigger public fallback`() {
        val first = ServerInfo("Other LAN", "http://192.168.1.20:8096")
        val selected = ServerInfo("MulletaFlix LAN", "http://192.168.1.10:8096")

        assertEquals(
            true,
            shouldTryFallbackAfterDiscoveryFailure(
                discovered = listOf(first, selected),
                failedEndpoint = selected.url,
            ),
        )
    }

    @Test
    fun `public endpoint failure does not start another fallback`() {
        val lan = ServerInfo("MulletaFlix LAN", "http://192.168.1.10:8096")

        assertEquals(
            false,
            shouldTryFallbackAfterDiscoveryFailure(
                discovered = listOf(lan),
                failedEndpoint = DEFAULT_MULLETAFLIX_SERVER_URL,
            ),
        )
    }
}
