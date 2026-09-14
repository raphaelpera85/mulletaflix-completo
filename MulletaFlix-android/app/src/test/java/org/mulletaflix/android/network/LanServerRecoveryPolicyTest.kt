package org.mulletaflix.android.network

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

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
    fun `authenticated session triggers a fresh LAN scan`() {
        assertTrue(shouldScanAfterAuthentication("user-123"))
        assertFalse(shouldScanAfterAuthentication(null))
        assertFalse(shouldScanAfterAuthentication("  "))
    }
}
