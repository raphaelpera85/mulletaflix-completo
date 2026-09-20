package org.mulletaflix.android.network

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.feature.auth.ServerInfo

class LanServerRecoveryPolicyTest {
    @Test
    fun `switches when discovery finds a different LAN endpoint`() {
        assertTrue(shouldSwitchToLan("http://mulletaflix.duckdns.org:8096", "http://192.168.1.20:8096"))
    }

    @Test
    fun `does not rewrite the same or blank endpoint`() {
        assertFalse(shouldSwitchToLan("http://192.168.1.20:8096/", "HTTP://192.168.1.20:8096"))
        assertFalse(shouldSwitchToLan("http://server:8096", ""))
    }

    @Test
    fun `does not oscillate when discovery adds whitespace and trailing slash`() {
        assertFalse(
            shouldSwitchToLan(
                currentUrl = " HTTP://192.168.1.20:8096 ",
                discoveredUrl = "http://192.168.1.20:8096/",
            ),
        )
    }

    @Test
    fun `authenticated session triggers a fresh LAN scan`() {
        assertTrue(shouldScanAfterAuthentication("user-123"))
        assertFalse(shouldScanAfterAuthentication(null))
        assertFalse(shouldScanAfterAuthentication("  "))
    }

    @Test
    fun `returns public endpoint when the previous LAN address disappears`() {
        assertTrue(
            publicFallbackAfterLanLoss(
                currentUrl = "http://192.168.1.20:8096",
                publicUrl = "http://mulletaflix.duckdns.org:8096",
            ) == "http://mulletaflix.duckdns.org:8096",
        )
    }

    @Test
    fun `does not replace a custom remote endpoint`() {
        assertTrue(
            publicFallbackAfterLanLoss(
                currentUrl = "https://media.example.com:443",
                publicUrl = "http://mulletaflix.duckdns.org:8096",
            ) == null,
        )
    }

    @Test
    fun `recognizes private IPv4 ranges but not public hosts`() {
        assertTrue(isLocalServerUrl("http://10.0.0.4:8096"))
        assertTrue(isLocalServerUrl("http://172.20.0.4:8096"))
        assertTrue(isLocalServerUrl("http://192.168.0.4:8096"))
        assertFalse(isLocalServerUrl("http://8.8.8.8:8096"))
    }

    @Test
    fun `selects the authenticated server when multiple LAN servers advertise`() {
        val other = ServerInfo("Outro", "http://192.168.1.10:8096", serverId = "other")
        val expected = ServerInfo("MulletaFlix", "http://192.168.1.20:8096", serverId = "mulletaflix")

        assertTrue(
            selectAuthenticatedLanServer(listOf(other, expected), "MULLETAFLIX") == expected,
        )
    }

    @Test
    fun `does not select another server when authenticated identity is known`() {
        val other = ServerInfo("Outro", "http://192.168.1.10:8096", serverId = "other")

        assertTrue(selectAuthenticatedLanServer(listOf(other), "mulletaflix") == null)
    }

    @Test
    fun `legacy session auto selects the only discovered server`() {
        val only = ServerInfo("MulletaFlix", "http://192.168.1.20:8096")

        assertTrue(selectAuthenticatedLanServer(listOf(only), null) == only)
    }

    @Test
    fun `legacy session does not guess between multiple discovered servers`() {
        val first = ServerInfo("Primeiro", "http://192.168.1.10:8096")
        val second = ServerInfo("Segundo", "http://192.168.1.20:8096")

        assertTrue(selectAuthenticatedLanServer(listOf(first, second), null) == null)
    }
}
