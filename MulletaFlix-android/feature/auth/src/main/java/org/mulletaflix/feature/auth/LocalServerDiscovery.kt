package org.mulletaflix.feature.auth

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.Inet4Address
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.NetworkInterface
import java.util.Collections

private const val DISCOVERY_PORT = 7359
private const val DISCOVERY_MESSAGE = "who is MulletaFlixServer?"

/** Discovers MulletaFlix/Jellyfin-compatible servers on the current LAN. */
class LocalServerDiscovery {

    suspend fun discover(timeoutMs: Int = 2_500): List<ServerInfo> = withContext(Dispatchers.IO) {
        val broadcastAddresses = networkBroadcastAddresses()

        val results = linkedMapOf<String, ServerInfo>()
        DatagramSocket().use { socket ->
            socket.broadcast = true
            socket.soTimeout = 250
            val request = DISCOVERY_MESSAGE.toByteArray(Charsets.UTF_8)
            val targets = (broadcastAddresses + InetAddress.getByName("255.255.255.255")).distinct()
            targets.forEach { target ->
                socket.send(DatagramPacket(request, request.size, target, DISCOVERY_PORT))
            }

            val deadline = System.currentTimeMillis() + timeoutMs
            while (System.currentTimeMillis() < deadline) {
                val buffer = ByteArray(4096)
                val packet = DatagramPacket(buffer, buffer.size)
                try {
                    socket.receive(packet)
                    parseResponse(String(packet.data, 0, packet.length, Charsets.UTF_8))?.let { server ->
                        results[server.url] = server
                    }
                } catch (_: java.net.SocketTimeoutException) {
                    // Short timeouts let us keep the discovery responsive while collecting replies.
                }
            }
        }
        results.values.toList()
    }

    private fun networkBroadcastAddresses(): List<InetAddress> = runCatching {
        Collections.list(NetworkInterface.getNetworkInterfaces())
            .asSequence()
            .filter { it.isUp && !it.isLoopback }
            .flatMap { networkInterface ->
                networkInterface.interfaceAddresses.asSequence()
                    .filter { it.address is Inet4Address && it.broadcast != null }
                    .mapNotNull { it.broadcast }
            }
            .distinct()
            .toList()
    }.getOrDefault(emptyList())

    private fun parseResponse(payload: String): ServerInfo? = runCatching {
        val json = JSONObject(payload)
        val address = json.optString("Address").ifBlank { return null }
        val url = normalizeServerUrl(address) ?: return null
        ServerInfo(
            name = json.optString("Name").ifBlank { "MulletaFlix Server" },
            url = url,
            version = null,
        )
    }.getOrNull()
}
