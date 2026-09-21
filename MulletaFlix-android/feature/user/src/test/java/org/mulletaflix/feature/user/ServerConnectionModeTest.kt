package org.mulletaflix.feature.user

import org.junit.Assert.assertEquals
import org.junit.Test

class ServerConnectionModeTest {
    @Test
    fun `uses the active endpoint and trims it before copying`() {
        assertEquals("http://192.168.1.20:8096", activeServerUrl("  http://192.168.1.20:8096  ", "https://remote"))
        assertEquals("https://remote", activeServerUrl(" ", " https://remote "))
    }

    @Test
    fun `recognizes private LAN addresses and local hostnames`() {
        assertEquals(ServerConnectionMode.Lan, classifyServerConnection("http://192.168.1.20:8096"))
        assertEquals(ServerConnectionMode.Lan, classifyServerConnection("http://mulletaflix.local:8096"))
        assertEquals(ServerConnectionMode.Lan, classifyServerConnection("http://10.0.0.4:8096"))
    }

    @Test
    fun `recognizes the configured public endpoint as internet`() {
        assertEquals(
            ServerConnectionMode.Internet,
            classifyServerConnection("HTTP://MULLETAFLIX.DUCKDNS.ORG:8096/"),
        )
    }

    @Test
    fun `keeps other valid hosts as remote and malformed input safe`() {
        assertEquals(ServerConnectionMode.Remote, classifyServerConnection("https://media.example.com:8096"))
        assertEquals(ServerConnectionMode.Remote, classifyServerConnection("not a url"))
        assertEquals("Rede local (LAN)", serverConnectionModeLabel(ServerConnectionMode.Lan))
    }
}
