package org.mulletaflix.android

import android.security.NetworkSecurityPolicy
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.net.InterfaceAddress
import java.net.InetSocketAddress
import java.net.NetworkInterface
import java.net.ServerSocket
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import okhttp3.OkHttpClient
import okhttp3.Request
import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.CleartextTrafficPolicy
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy

@RunWith(AndroidJUnit4::class)
class AndroidCleartextPolicyIntegrationTest {
    @Test
    fun activeLanPrefixAllowsPeerAndRejectsOffSubnetPrivateAddress() {
        val activeSubnets = activeLanSubnets()
        val activeIpv4Subnet = activeSubnets.firstOrNull { subnet ->
            subnet.address.address.size == 4 && subnet.networkPrefixLength.toInt() in 1..30 &&
                isPrivateIpv4(subnet.address.address)
        }
        assertNotNull("An active private IPv4 LAN prefix must be available in the emulator", activeIpv4Subnet)

        val selectedSubnet = requireNotNull(activeIpv4Subnet)
        val localAddress = ipv4ToLong(selectedSubnet.address.address)
        val prefixLength = selectedSubnet.networkPrefixLength.toInt()
        val hostMask = (1L shl (32 - prefixLength)) - 1
        val networkMask = IPV4_MASK xor hostMask
        val network = localAddress and networkMask
        val broadcast = network or hostMask
        val peer = listOf(localAddress + 1, localAddress - 1)
            .firstOrNull { it > network && it < broadcast && it != localAddress }
        assertNotNull("The active emulator subnet must contain a peer host", peer)

        val peerUrl = "http://${longToIpv4(requireNotNull(peer))}:8096"
        assertTrue(
            "HTTP must be allowed to a peer inside the active LAN prefix",
            CleartextTrafficPolicy.isAllowed(peerUrl),
        )

        val offSubnetPrivateAddress = privateIpv4Candidates().firstOrNull { candidate ->
            activeSubnets.none { subnet -> isInSubnet(candidate, subnet) }
        }
        assertNotNull("A private IPv4 address outside active prefixes must be available", offSubnetPrivateAddress)
        assertFalse(
            "HTTP must be denied for a private address outside every active LAN prefix",
            CleartextTrafficPolicy.isAllowed("http://${longToIpv4(requireNotNull(offSubnetPrivateAddress))}:8096"),
        )
    }

    @Test
    fun protectedClientCompletesHttpRequestOnTheActiveLanInterface() {
        val activeAddress = activeLanSubnets().firstOrNull { subnet ->
            subnet.address.address.size == 4 && isPrivateIpv4(subnet.address.address)
        }?.address
        assertNotNull("An active private IPv4 LAN address must be available", activeAddress)
        val lanAddress = requireNotNull(activeAddress)
        val host = lanAddress.hostAddress ?: error("The active LAN address has no printable host")
        val serverFailure = AtomicReference<Throwable?>()

        ServerSocket().use { server ->
            server.soTimeout = 5_000
            server.bind(InetSocketAddress(lanAddress, 0), 1)
            val responder = Thread {
                runCatching {
                    server.accept().use { socket ->
                        socket.soTimeout = 5_000
                        val reader = socket.getInputStream().bufferedReader()
                        while (true) {
                            val line = reader.readLine() ?: break
                            if (line.isEmpty()) break
                        }
                        socket.getOutputStream().write(
                            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok".toByteArray(),
                        )
                        socket.getOutputStream().flush()
                    }
                }.onFailure(serverFailure::set)
            }.apply {
                name = "cleartext-lan-test-responder"
                isDaemon = true
                start()
            }

            val client = OkHttpClient.Builder()
                .callTimeout(5, TimeUnit.SECONDS)
                .enforceLocalNetworkCleartextPolicy()
                .build()
            client.newCall(
                Request.Builder()
                    .url("http://$host:${server.localPort}/health")
                    .build(),
            ).execute().use { response ->
                assertEquals(200, response.code)
                assertEquals("ok", response.body?.string())
            }

            responder.join(5_000)
            assertFalse("The local test responder must finish", responder.isAlive)
            assertNull(serverFailure.get())
        }
    }

    @Test
    fun packagedPlatformPolicyAndApplicationGuardPreserveTheLanContract() {
        val platformPolicy = NetworkSecurityPolicy.getInstance()

        assertFalse(
            "The public MulletaFlix host must require HTTPS in the packaged network security config",
            platformPolicy.isCleartextTrafficPermitted("mulletaflix.duckdns.org"),
        )
        assertFalse(
            "The HTTPS-only rule must include public subdomains",
            platformPolicy.isCleartextTrafficPermitted("api.mulletaflix.duckdns.org"),
        )
        assertTrue(
            "Android must allow loopback HTTP for isolated LAN transport tests",
            platformPolicy.isCleartextTrafficPermitted("127.0.0.1"),
        )

        assertTrue(CleartextTrafficPolicy.isAllowed("https://mulletaflix.duckdns.org"))
        assertTrue(CleartextTrafficPolicy.isAllowed("http://127.0.0.1:8096"))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://mulletaflix.duckdns.org:8096"))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://8.8.8.8:8096"))
    }

    private fun activeLanSubnets(): List<InterfaceAddress> {
        val interfaces = NetworkInterface.getNetworkInterfaces() ?: return emptyList()
        return buildList {
            while (interfaces.hasMoreElements()) {
                val networkInterface = interfaces.nextElement()
                val eligible = runCatching {
                    networkInterface.isUp && !networkInterface.isLoopback && !networkInterface.isPointToPoint
                }.getOrDefault(false)
                if (eligible) addAll(networkInterface.interfaceAddresses)
            }
        }
    }

    private fun isPrivateIpv4(address: ByteArray): Boolean {
        if (address.size != 4) return false
        val first = address[0].toInt() and 0xff
        val second = address[1].toInt() and 0xff
        return first == 10 || (first == 172 && second in 16..31) ||
            (first == 192 && second == 168) || (first == 169 && second == 254)
    }

    private fun isInSubnet(candidate: Long, subnet: InterfaceAddress): Boolean {
        val bytes = subnet.address.address
        val prefix = subnet.networkPrefixLength.toInt()
        if (bytes.size != 4 || prefix !in 1..32) return false
        val mask = IPV4_MASK shl (32 - prefix) and IPV4_MASK
        return (candidate and mask) == (ipv4ToLong(bytes) and mask)
    }

    private fun ipv4ToLong(address: ByteArray): Long = address.fold(0L) { value, octet ->
        (value shl 8) or (octet.toLong() and 0xff)
    }

    private fun longToIpv4(address: Long): String = (3 downTo 0)
        .joinToString(".") { shift -> ((address shr (shift * 8)) and 0xff).toString() }

    private fun privateIpv4Candidates(): Sequence<Long> = sequence {
        for (octet in 1..254 step 7) {
            yield(ipv4ToLong(byteArrayOf(10, octet.toByte(), 77, 91)))
            yield(ipv4ToLong(byteArrayOf(172.toByte(), (16 + octet % 16).toByte(), 77, 91)))
            yield(ipv4ToLong(byteArrayOf(192.toByte(), 168.toByte(), octet.toByte(), 91)))
            yield(ipv4ToLong(byteArrayOf(169.toByte(), 254.toByte(), octet.toByte(), 91)))
        }
    }

    private companion object {
        const val IPV4_MASK = 0xffffffffL
    }
}
