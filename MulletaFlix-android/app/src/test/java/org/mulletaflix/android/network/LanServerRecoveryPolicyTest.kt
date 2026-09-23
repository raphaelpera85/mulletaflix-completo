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
                consecutiveMisses = 2,
            ) == "http://mulletaflix.duckdns.org:8096",
        )
    }

    @Test
    fun `does not fall back after a single transient LAN miss`() {
        assertTrue(
            publicFallbackAfterLanLoss(
                currentUrl = "http://192.168.1.20:8096",
                publicUrl = "http://mulletaflix.duckdns.org:8096",
                consecutiveMisses = 1,
            ) == null,
        )
    }

    @Test
    fun `falls back from a local endpoint even when it has a trailing slash`() {
        assertTrue(
            publicFallbackAfterLanLoss(
                currentUrl = "http://192.168.1.20:8096/",
                publicUrl = "http://mulletaflix.duckdns.org:8096",
                consecutiveMisses = 2,
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
    fun `recognizes private IPv6 LAN addresses and keeps unspecified address non dialable`() {
        assertTrue(isLocalServerUrl("http://[fd12:3456::20]:8096"))
        assertTrue(isLocalServerUrl("http://[fe80::20]:8096"))
        assertTrue(isLocalServerUrl("http://[::]:8096"))
        assertFalse(isDialableServerUrl("http://[::]:8096"))
        assertTrue(isDialableServerUrl("http://[fd12:3456::20]:8096"))
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

    @Test
    fun `never switches the saved server to a non local address`() {
        // The locality check was missing in this direction while the opposite
        // one had it, so any host that answered the discovery probe could replace
        // the saved endpoint — and every later request, Authorization header
        // included, went to that address.
        assertFalse(
            shouldSwitchToLan("http://192.168.1.20:8096", "http://203.0.113.9:8096"),
        )
        assertFalse(
            shouldSwitchToLan("http://mulletaflix.duckdns.org:8096", "http://8.8.8.8:8096"),
        )
    }

    @Test
    fun `never switches to a listen-only address`() {
        // `0.0.0.0` means "any local address": a server advertising it cannot be
        // dialled, and it is classified as local, so it needs its own guard.
        assertFalse(
            shouldSwitchToLan("http://192.168.1.20:8096", "http://0.0.0.0:8096"),
        )
        assertTrue(isLocalServerUrl("http://0.0.0.0:8096"))
        assertFalse(isDialableServerUrl("http://0.0.0.0:8096"))
        assertTrue(isDialableServerUrl("http://192.168.1.20:8096"))
    }

    @Test
    fun `a public responder is not a LAN candidate`() {
        val remote = ServerInfo("Remoto", "http://203.0.113.9:8096")

        assertTrue(selectAuthenticatedLanServer(listOf(remote), null) == null)
    }

    @Test
    fun `a listen-only responder is not a LAN candidate`() {
        val wildcard = ServerInfo("Curinga", "http://0.0.0.0:8096")

        assertTrue(selectAuthenticatedLanServer(listOf(wildcard), null) == null)
    }

    @Test
    fun `only the latest active scan can apply its endpoint`() {
        assertTrue(isCurrentLanScan(scanGeneration = 4, latestGeneration = 4, isStarted = true))
        assertFalse(isCurrentLanScan(scanGeneration = 3, latestGeneration = 4, isStarted = true))
        assertFalse(isCurrentLanScan(scanGeneration = 4, latestGeneration = 4, isStarted = false))
    }
}
