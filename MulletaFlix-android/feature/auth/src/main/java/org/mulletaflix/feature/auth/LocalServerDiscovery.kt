package org.mulletaflix.feature.auth

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.wifi.WifiManager
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import java.net.Inet4Address
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.NetworkInterface
import java.util.Collections
import javax.inject.Inject

private const val DISCOVERY_PORT = 7359
private const val DISCOVERY_MESSAGE = "who is MulletaFlixServer?"
private const val DISCOVERY_RETRY_INTERVAL_MS = 750
private const val DISCOVERY_MAX_WINDOW_MS = 10_000

/** Discovers MulletaFlix/Jellyfin-compatible servers on the current LAN. */
class LocalServerDiscovery @Inject constructor(
    @ApplicationContext context: Context,
) {
    private val wifiManager = context.applicationContext.getSystemService(WifiManager::class.java)
    private val connectivityManager = context.applicationContext.getSystemService(ConnectivityManager::class.java)

    suspend fun discover(timeoutMs: Int = 2_500): List<ServerInfo> = withContext(Dispatchers.IO) {
        val boundedTimeoutMs = boundedDiscoveryTimeoutMs(timeoutMs)
        val broadcastAddresses = networkBroadcastAddresses()

        val results = linkedMapOf<String, ServerInfo>()
        withWifiMulticastLock {
            val sockets = createDiscoverySockets()
            try {
                sockets.forEach { socket ->
                    socket.broadcast = true
                    socket.reuseAddress = true
                }
                val request = DISCOVERY_MESSAGE.toByteArray(Charsets.UTF_8)
                val targets = (broadcastAddresses + InetAddress.getByName("255.255.255.255")).distinct()
                val deadline = System.currentTimeMillis() + boundedTimeoutMs
                val probeDelays = discoveryProbeDelays(boundedTimeoutMs)
                var probeIndex = 0
                var nextProbeAt = System.currentTimeMillis()
                while (System.currentTimeMillis() < deadline) {
                    val now = System.currentTimeMillis()
                    if (probeIndex < probeDelays.size && now >= nextProbeAt) {
                        sockets.forEach { socket ->
                            targets.forEach { target ->
                                runCatching {
                                    socket.send(DatagramPacket(request, request.size, target, DISCOVERY_PORT))
                                }
                            }
                        }
                        probeIndex += 1
                        nextProbeAt = System.currentTimeMillis() +
                            (probeDelays.getOrNull(probeIndex)?.minus(probeDelays[probeIndex - 1])
                                ?: DISCOVERY_RETRY_INTERVAL_MS)
                    }
                    sockets.forEach { socket ->
                        if (System.currentTimeMillis() >= deadline) return@forEach
                        val buffer = ByteArray(4096)
                        val packet = DatagramPacket(buffer, buffer.size)
                        socket.soTimeout = minOf(
                            100,
                            (deadline - System.currentTimeMillis()).coerceAtLeast(1L).toInt(),
                        )
                        try {
                            socket.receive(packet)
                            parseDiscoveryResponse(String(packet.data, 0, packet.length, Charsets.UTF_8))?.let { server ->
                                results[server.url] = server
                            }
                        } catch (_: java.net.SocketTimeoutException) {
                            // Short timeouts let us keep discovery responsive across all local interfaces.
                        }
                    }
                }
            } finally {
                sockets.forEach { socket -> runCatching { socket.close() } }
            }
        }
        results.values.toList()
    }

    /**
     * Creates one UDP socket per eligible local transport. Android otherwise
     * routes a process-wide broadcast through only its default network, which
     * can miss a MulletaFlix server when Wi-Fi/Ethernet or VPN interfaces
     * coexist. A plain socket remains the compatibility fallback for devices
     * where ConnectivityManager does not expose a usable local network.
     */
    @Suppress("DEPRECATION") // allNetworks keeps the Android 24 compatibility path for local-only transports.
    private fun createDiscoverySockets(): List<DatagramSocket> {
        val localNetworks = connectivityManager?.allNetworks.orEmpty()
            .filter { network ->
                val capabilities = connectivityManager?.getNetworkCapabilities(network)
                capabilities?.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) == true ||
                    capabilities?.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) == true
            }
            .distinct()

        val boundSockets = localNetworks.mapNotNull { network ->
            runCatching {
                DatagramSocket().also { socket -> network.bindSocket(socket) }
            }.getOrNull()
        }
        return boundSockets.ifEmpty { listOf(DatagramSocket()) }
    }

    /**
     * Android may filter LAN broadcast/multicast packets while the device is
     * idle. Holding this lock only for the short discovery window keeps LAN
     * auto-selection reliable without keeping Wi-Fi awake permanently.
     */
    private inline fun <T> withWifiMulticastLock(block: () -> T): T {
        val lock = runCatching {
            wifiManager?.createMulticastLock("mulletaflix-lan-discovery")?.apply {
                setReferenceCounted(false)
                acquire()
            }
        }.getOrNull()
        return try {
            block()
        } finally {
            runCatching {
                if (lock?.isHeld == true) lock.release()
            }
        }
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

}

/** Prevents a caller from holding LAN sockets and the Wi-Fi lock indefinitely. */
internal fun boundedDiscoveryTimeoutMs(requestedTimeoutMs: Int): Int =
    requestedTimeoutMs.coerceIn(0, DISCOVERY_MAX_WINDOW_MS)

/** Returns retry offsets without exceeding the discovery window. */
internal fun discoveryProbeDelays(
    timeoutMs: Int,
    retryIntervalMs: Int = DISCOVERY_RETRY_INTERVAL_MS,
): List<Int> {
    val timeout = timeoutMs.coerceAtLeast(0)
    val interval = retryIntervalMs.coerceAtLeast(1)
    if (timeout == 0) return emptyList()
    return generateSequence(0) { previous -> previous + interval }
        .takeWhile { it < timeout }
        .toList()
}

/** Parses the Jellyfin/MulletaFlix UDP discovery payload safely. */
internal fun parseDiscoveryResponse(payload: String): ServerInfo? = runCatching {
    val json = Json.parseToJsonElement(payload).jsonObject
    val address = json["Address"]?.jsonPrimitive?.contentOrNull?.ifBlank { null } ?: return null
    val url = normalizeServerUrl(address) ?: return null
    ServerInfo(
        name = json["Name"]?.jsonPrimitive?.contentOrNull?.ifBlank { null } ?: "MulletaFlix Server",
        url = url,
        version = json["Version"]?.jsonPrimitive?.contentOrNull?.ifBlank { null },
        serverId = (json["Id"] ?: json["ServerId"])?.jsonPrimitive?.contentOrNull?.ifBlank { null },
    )
}.getOrNull()
