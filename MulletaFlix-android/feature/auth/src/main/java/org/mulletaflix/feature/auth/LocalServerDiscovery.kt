package org.mulletaflix.feature.auth

import android.content.Context
import android.net.wifi.WifiManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.URI

private const val DISCOVERY_PORT = 7359
private const val DISCOVERY_MESSAGE = "who is MulletaFlixServer?"

/** Discovers MulletaFlix/Jellyfin-compatible servers on the current LAN. */
class LocalServerDiscovery(private val context: Context) {

    suspend fun discover(timeoutMs: Int = 2_500): List<ServerInfo> = withContext(Dispatchers.IO) {
        val wifiManager = context.applicationContext
            .getSystemService(Context.WIFI_SERVICE) as? WifiManager
            ?: return@withContext emptyList()

        val broadcastAddress = wifiManager.dhcpInfo?.let { dhcpInfo ->
            if (dhcpInfo.ipAddress != 0 && dhcpInfo.netmask != 0) {
                val broadcast = (dhcpInfo.ipAddress and dhcpInfo.netmask) or dhcpInfo.netmask.inv()
                InetAddress.getByAddress(
                    byteArrayOf(
                        (broadcast and 0xff).toByte(),
                        (broadcast shr 8 and 0xff).toByte(),
                        (broadcast shr 16 and 0xff).toByte(),
                        (broadcast shr 24 and 0xff).toByte(),
                    )
                )
            } else {
                null
            }
        } ?: InetAddress.getByName("255.255.255.255")

        val results = linkedMapOf<String, ServerInfo>()
        DatagramSocket().use { socket ->
            socket.broadcast = true
            socket.soTimeout = 250
            val request = DISCOVERY_MESSAGE.toByteArray(Charsets.UTF_8)
            val targets = listOf(broadcastAddress, InetAddress.getByName("255.255.255.255")).distinct()
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

    private fun parseResponse(payload: String): ServerInfo? = runCatching {
        val json = JSONObject(payload)
        val address = json.optString("Address").ifBlank { return null }
        val uri = URI(address)
        if (uri.scheme.isNullOrBlank() || uri.host.isNullOrBlank()) return null
        val url = address.trimEnd('/')
        ServerInfo(
            name = json.optString("Name").ifBlank { "MulletaFlix Server" },
            url = url,
            version = null,
        )
    }.getOrNull()
}
