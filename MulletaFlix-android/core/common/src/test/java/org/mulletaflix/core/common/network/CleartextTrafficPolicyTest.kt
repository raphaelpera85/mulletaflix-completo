package org.mulletaflix.core.common.network

import okhttp3.HttpUrl.Companion.toHttpUrl
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class CleartextTrafficPolicyTest {
    @Test
    fun `HTTPS remains valid for remote servers`() {
        assertTrue(CleartextTrafficPolicy.isAllowed("https://media.example.org"))
    }

    @Test
    fun `HTTP is allowed for loopback private link local and local DNS names`() {
        listOf(
            "127.0.0.1",
            "127.42.10.9",
            "10.0.0.8",
            "172.20.0.8",
            "192.168.1.8",
            "169.254.4.2",
            "::1",
            "fd12:3456::8",
            "fe80::8",
            "mulletaflix.local",
            "media.lan",
            "mulletaflix.home.arpa",
        ).forEach { host ->
            assertTrue("expected local host: $host", CleartextTrafficPolicy.isAllowed("http://$host"))
        }
    }

    @Test
    fun `HTTP is denied for public and unspecified hosts`() {
        listOf(
            "http://mulletaflix.duckdns.org:8096",
            "http://8.8.8.8:8096",
            "http://203.0.113.8:8096",
            "http://0.0.0.0:8096",
            "http://[::]:8096",
        ).forEach { url -> assertFalse("expected cleartext denial: $url", CleartextTrafficPolicy.isAllowed(url)) }
    }

    @Test
    fun `network interceptor permits an explicitly local request`() {
        val localUrl = "http://192.168.1.20:8096/Items".toHttpUrl()
        CleartextTrafficPolicy.requireAllowed(localUrl)
    }
}
