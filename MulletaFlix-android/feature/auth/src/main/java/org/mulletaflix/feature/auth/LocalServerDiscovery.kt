package org.mulletaflix.feature.auth

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.wifi.WifiManager
import android.os.SystemClock
import android.os.Build
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
    private val appContext = context.applicationContext
    private val wifiManager = appContext.getSystemService(WifiManager::class.java)
    private val connectivityManager = appContext.getSystemService(ConnectivityManager::class.java)

    suspend fun discover(timeoutMs: Int = 2_500): List<ServerInfo> = withContext(Dispatchers.IO) {
        if (requiresLocalNetworkPermission(
                sdkInt = Build.VERSION.SDK_INT,
                targetSdk = appContext.applicationInfo.targetSdkVersion,
                permissionGranted = appContext.checkSelfPermission(Manifest.permission.ACCESS_LOCAL_NETWORK) ==
                    PackageManager.PERMISSION_GRANTED,
            )
        ) {
            throw LocalNetworkPermissionRequiredException()
        }
        val boundedTimeoutMs = boundedDiscoveryTimeoutMs(timeoutMs)
        val broadcastAddresses = networkBroadcastAddresses()

        val results = linkedMapOf<String, ServerInfo>()
        withWifiMulticastLock {
            val sockets = createDiscoverySockets()
            try {
                sockets.receiveSockets.forEach { socket ->
                    socket.broadcast = true
                    socket.reuseAddress = true
                }
                val request = DISCOVERY_MESSAGE.toByteArray(Charsets.UTF_8)
                val targets = (broadcastAddresses + InetAddress.getByName("255.255.255.255")).distinct()
                val discoveryWindow = DiscoveryWindow(boundedTimeoutMs) { SystemClock.elapsedRealtime() }
                val probeDelays = discoveryProbeDelays(boundedTimeoutMs)
                var probeIndex = 0
                var nextProbeAt = discoveryWindow.nowElapsedRealtimeMs()
                while (discoveryWindow.isOpen()) {
                    val now = discoveryWindow.nowElapsedRealtimeMs()
                    if (probeIndex < probeDelays.size && now >= nextProbeAt) {
                        sendDiscoveryProbe(
                            targetCount = targets.size,
                            boundSocketCount = sockets.boundSockets.size,
                            sendBound = { socketIndex, targetIndex ->
                                sendProbe(sockets.boundSockets[socketIndex], request, targets[targetIndex])
                            },
                            sendFallback = { targetIndex ->
                                val fallback = sockets.fallbackSocket
                                    ?: sockets.primarySocket.takeIf { sockets.boundSockets.isEmpty() }
                                fallback?.let { sendProbe(it, request, targets[targetIndex]) } ?: false
                            },
                        )
                        probeIndex += 1
                        nextProbeAt = discoveryWindow.nowElapsedRealtimeMs() +
                            (probeDelays.getOrNull(probeIndex)?.minus(probeDelays[probeIndex - 1])
                                ?: DISCOVERY_RETRY_INTERVAL_MS)
                    }
                    sockets.receiveSockets.forEach { socket ->
                        if (!discoveryWindow.isOpen()) return@forEach
                        val buffer = ByteArray(4096)
                        val packet = DatagramPacket(buffer, buffer.size)
                        socket.soTimeout = minOf(
                            100,
                            discoveryWindow.remainingSocketTimeoutMs(),
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
                sockets.receiveSockets.forEach { socket -> runCatching { socket.close() } }
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
    private fun createDiscoverySockets(): DiscoverySockets {
        val localNetworks = connectivityManager?.allNetworks.orEmpty()
            .filter { network ->
                val capabilities = connectivityManager?.getNetworkCapabilities(network)
                capabilities?.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) == true ||
                    capabilities?.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) == true
            }
            .distinct()

        val boundSockets = localNetworks.mapNotNull { network ->
            val socket = runCatching { DatagramSocket() }.getOrNull() ?: return@mapNotNull null
            try {
                network.bindSocket(socket)
                socket
            } catch (_: Exception) {
                runCatching { socket.close() }
                null
            }
        }
        if (boundSockets.isEmpty()) {
            val primarySocket = DatagramSocket()
            return DiscoverySockets(emptyList(), null, primarySocket, listOf(primarySocket))
        }
        val fallbackSocket = runCatching { DatagramSocket() }.getOrNull()
        return DiscoverySockets(
            boundSockets = boundSockets,
            fallbackSocket = fallbackSocket,
            primarySocket = boundSockets.first(),
            receiveSockets = boundSockets + listOfNotNull(fallbackSocket),
        )
    }

    private fun sendProbe(socket: DatagramSocket, request: ByteArray, target: InetAddress): Boolean =
        runCatching { socket.send(DatagramPacket(request, request.size, target, DISCOVERY_PORT)) }.isSuccess

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

internal class LocalNetworkPermissionRequiredException : SecurityException(
    "Permissão de acesso à rede local necessária para descobrir servidores.",
)

private data class DiscoverySockets(
    val boundSockets: List<DatagramSocket>,
    val fallbackSocket: DatagramSocket?,
    val primarySocket: DatagramSocket,
    val receiveSockets: List<DatagramSocket>,
)

/** Prevents a caller from holding LAN sockets and the Wi-Fi lock indefinitely. */
internal fun boundedDiscoveryTimeoutMs(requestedTimeoutMs: Int): Int =
    requestedTimeoutMs.coerceIn(0, DISCOVERY_MAX_WINDOW_MS)

/** Shares the monotonic clock between probe scheduling, loop bounds, and socket waits. */
internal class DiscoveryWindow(
    timeoutMs: Int,
    private val elapsedRealtimeMs: () -> Long,
) {
    private val deadlineElapsedRealtimeMs =
        elapsedRealtimeMs() + boundedDiscoveryTimeoutMs(timeoutMs)

    fun nowElapsedRealtimeMs(): Long = elapsedRealtimeMs()

    fun isOpen(): Boolean = nowElapsedRealtimeMs() < deadlineElapsedRealtimeMs

    fun remainingSocketTimeoutMs(): Int =
        (deadlineElapsedRealtimeMs - nowElapsedRealtimeMs())
            .coerceAtLeast(1L)
            .coerceAtMost(Int.MAX_VALUE.toLong())
            .toInt()
}

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

/** Sends to every bound socket per target; fallback covers only targets where all fail. */
internal fun sendDiscoveryProbe(
    targetCount: Int,
    boundSocketCount: Int,
    sendBound: (socketIndex: Int, targetIndex: Int) -> Boolean,
    sendFallback: (targetIndex: Int) -> Boolean,
): Boolean {
    var anySendSucceeded = false
    repeat(targetCount) { targetIndex ->
        var targetSentOnBoundSocket = false
        repeat(boundSocketCount) { socketIndex ->
            if (sendBound(socketIndex, targetIndex)) targetSentOnBoundSocket = true
        }
        if (targetSentOnBoundSocket) {
            anySendSucceeded = true
        } else if (sendFallback(targetIndex)) {
            anySendSucceeded = true
        }
    }
    return anySendSucceeded
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
