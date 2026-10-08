package org.mulletaflix.core.common.network

import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.Proxy
import java.net.ServerSocket
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.OkHttpClient
import okhttp3.Request
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class CleartextTrafficPolicyTest {
    @Test
    fun `HTTPS remains valid for remote servers`() {
        assertTrue(CleartextTrafficPolicy.isAllowed("https://media.example.org"))
    }

    @Test
    fun `HTTP is allowed for loopback and local DNS names`() {
        listOf(
            "127.0.0.1",
            "127.42.10.9",
            "mulletaflix.local",
            "mulletaflix.home.arpa",
        ).forEach { host ->
            val urlHost = if (host.contains(':')) "[$host]" else host
            assertTrue("expected local host: $host", CleartextTrafficPolicy.isAllowed("http://$urlHost"))
        }
    }

    @Test
    fun `HTTP is denied for public unspecified and off subnet hosts`() {
        listOf(
            "http://mulletaflix.duckdns.org:8096",
            "http://8.8.8.8:8096",
            "http://203.0.113.8:8096",
            "http://0.0.0.0:8096",
            "http://[::]:8096",
            "http://media.lan:8096",
        ).forEach { url -> assertFalse("expected cleartext denial: $url", CleartextTrafficPolicy.isAllowed(url)) }
    }

    @Test
    fun `HTTP private addresses are allowed only inside an active subnet`() {
        val subnet = subnet("192.168.50.1", 24)

        assertTrue(CleartextTrafficPolicy.isAllowed("http://192.168.50.42:8096/subtitles/16.vtt", listOf(subnet)))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://192.168.51.20:8096/Items", listOf(subnet)))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://10.0.0.8:8096/Items", listOf(subnet)))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://10.20.30.40:8096/Items", listOf(subnet)))
    }

    @Test
    fun `IPv6 local address must match connected prefix`() {
        val subnet = subnet("fd12:3456:789a::1", 64)

        assertTrue(CleartextTrafficPolicy.isAllowed("http://[fd12:3456:789a::8]:8096", listOf(subnet)))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://[fd12:3456:789b::8]:8096", listOf(subnet)))
        assertTrue(CleartextTrafficPolicy.isAllowed("http://[fe80::8]:8096", listOf(subnet("fe80::1", 64))))
    }

    @Test
    fun `connected route validates resolved local DNS names against the socket subnet`() {
        val subnet = subnet("192.168.1.10", 24)
        val localAddress = InetAddress.getByName("192.168.1.10")

        assertTrue(
            CleartextTrafficPolicy.isAllowedOnConnectedRoute(
                "http://media.local:8096".toHttpUrl(),
                localAddress,
                InetAddress.getByName("192.168.1.20"),
                listOf(subnet),
            ),
        )
        assertFalse(
            CleartextTrafficPolicy.isAllowedOnConnectedRoute(
                "http://media.local:8096".toHttpUrl(),
                localAddress,
                InetAddress.getByName("192.168.2.20"),
                listOf(subnet),
            ),
        )
        assertFalse(
            CleartextTrafficPolicy.isAllowedOnConnectedRoute(
                "http://media.home.arpa:8096".toHttpUrl(),
                localAddress,
                InetAddress.getByName("8.8.8.8"),
                listOf(subnet),
            ),
        )
    }

    @Test
    fun `subnet matcher rejects invalid prefixes and address families`() {
        val ipv4Subnet = subnet("192.168.1.10", 24)

        assertFalse(ipv4Subnet.contains(InetAddress.getByName("192.168.1.11").address + byteArrayOf(1)))
        assertFalse(subnet("192.168.1.10", 0).contains(InetAddress.getByName("192.168.1.11").address))
        assertFalse(subnet("192.168.1.10", 33).contains(InetAddress.getByName("192.168.1.11").address))
    }

    @Test
    fun `shared client builder installs policy before app code and on network exchanges`() {
        val client = OkHttpClient.Builder()
            .enforceLocalNetworkCleartextPolicy()
            .build()

        assertTrue(client.interceptors.any { it is LocalNetworkCleartextInterceptor && !it.requireHttpsRedirects })
        assertTrue(client.networkInterceptors.contains(LocalNetworkCleartextNetworkInterceptor))
        assertFalse(client.followRedirects)
        assertFalse(client.followSslRedirects)
    }

    @Test
    fun `shared client can require HTTPS for every redirect`() {
        val client = OkHttpClient.Builder()
            .enforceLocalNetworkCleartextPolicy(requireHttpsRedirects = true)
            .build()

        assertTrue(client.interceptors.any { it is LocalNetworkCleartextInterceptor && it.requireHttpsRedirects })
        assertFalse(client.followRedirects)
        assertFalse(client.followSslRedirects)
    }

    @Test
    fun `HTTP request through proxy is rejected before request bytes are sent`() {
        ServerSocket(0, 1, InetAddress.getByName("127.0.0.1")).use { proxySocket ->
            proxySocket.soTimeout = 5_000
            val firstProxyByte = AtomicInteger(-2)
            val accepted = CountDownLatch(1)
            val proxyPeer = Thread {
                try {
                    proxySocket.accept().use { connection ->
                        accepted.countDown()
                        connection.soTimeout = 1_000
                        firstProxyByte.set(connection.getInputStream().read())
                    }
                } catch (_: java.io.IOException) {
                    // The test fails below if OkHttp never reaches the local proxy.
                }
            }.apply {
                isDaemon = true
                start()
            }

            val client = OkHttpClient.Builder()
                .proxy(Proxy(Proxy.Type.HTTP, InetSocketAddress("127.0.0.1", proxySocket.localPort)))
                .enforceLocalNetworkCleartextPolicy()
                .build()

            val failure = runCatching {
                client.newCall(Request.Builder().url("http://127.0.0.1:8096/health").build()).execute().use { }
            }.exceptionOrNull()

            proxyPeer.join(5_000)
            assertTrue("OkHttp should connect to the configured proxy", accepted.await(0, TimeUnit.MILLISECONDS))
            assertTrue("The proxy connection should be rejected", failure is java.io.IOException)
            assertFalse("The test proxy must finish", proxyPeer.isAlive)
            assertTrue("The proxy connection should close without receiving request bytes", firstProxyByte.get() == -1)
        }
    }

    private fun subnet(address: String, prefixLength: Int) =
        LocalNetworkSubnet(InetAddress.getByName(address).address, prefixLength)
}
