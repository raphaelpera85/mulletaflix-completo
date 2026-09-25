package org.mulletaflix.android.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkRequest
import android.net.NetworkCapabilities
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.feature.auth.LocalServerDiscovery
import org.mulletaflix.feature.auth.DEFAULT_MULLETAFLIX_SERVER_URL
import java.util.concurrent.atomic.AtomicLong
import javax.inject.Inject
import javax.inject.Singleton

private const val LAN_SCAN_DEBOUNCE_MS = 350L

/**
 * Re-checks the advertised MulletaFlix server after connectivity changes.
 * Authentication remains in DataStore; only the active base URL is replaced.
 */
@Singleton
class LanServerRecovery @Inject constructor(
    @ApplicationContext context: Context,
    private val sessionRepository: SessionRepository,
    private val discovery: LocalServerDiscovery,
) {
    private val connectivityManager = context.getSystemService(ConnectivityManager::class.java)
    private val scope = CoroutineScope(Dispatchers.IO)
    private val scanMutex = Mutex()
    private var scanJob: Job? = null
    private var sessionJob: Job? = null
    @Volatile private var started = false
    private var registered = false
    private val scanGeneration = AtomicLong(0L)
    private var consecutiveLanMisses = 0

    private val callback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) {
            scheduleScan()
        }

        override fun onLost(network: Network) {
            // A LAN endpoint may disappear while the activity remains open.
            // Re-scan immediately so the session can fall back to the public
            // server instead of keeping a stale private address.
            scheduleScan()
        }
    }

    fun start() {
        if (started) return
        started = true
        registered = runCatching {
            connectivityManager.registerNetworkCallback(
                NetworkRequest.Builder()
                    .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                    .build(),
                callback,
            )
            true
        }.getOrDefault(false)
        // Authentication-driven discovery must not depend on the optional
        // ConnectivityManager callback. Some devices reject callback
        // registration even though UDP discovery still works normally.
        sessionJob = scope.launch {
            sessionRepository.getCurrentUserId()
                .distinctUntilChanged()
                .collect { userId ->
                    if (shouldScanAfterAuthentication(userId)) {
                        scheduleScan()
                    }
                }
            }
        scheduleScan()
    }

    /** Re-checks LAN reachability when the activity returns to the foreground. */
    fun refresh() {
        if (started) scheduleScan()
    }

    fun stop() {
        if (!started) return
        started = false
        scanGeneration.incrementAndGet()
        if (registered) {
            registered = false
            runCatching { connectivityManager.unregisterNetworkCallback(callback) }
        }
        scanJob?.cancel()
        scanJob = null
        sessionJob?.cancel()
        sessionJob = null
    }

    private fun scheduleScan() {
        if (!started) return
        val generation = scanGeneration.incrementAndGet()
        scanJob?.cancel()
        scanJob = scope.launch {
            // Wi-Fi/Ethernet changes can emit several callbacks in quick
            // succession. Wait for the burst to settle before opening UDP
            // sockets; only the newest foreground scan should reach discovery.
            if (!shouldRunDebouncedLanScan(
                    scanGeneration = generation,
                    latestGeneration = { scanGeneration.get() },
                    isStarted = { started },
                    debounceMs = LAN_SCAN_DEBOUNCE_MS,
                )
            ) return@launch
            scanMutex.withLock {
                if (!isCurrentLanScan(generation, scanGeneration.get(), started)) return@withLock
                val userId = sessionRepository.getCurrentUserId().first() ?: return@withLock
                val currentUrl = sessionRepository.getBaseUrl().first()
                val authenticatedServerId = sessionRepository.getServerId().first()
                val localServer = selectAuthenticatedLanServer(
                    discovered = discovery.discover(timeoutMs = 2_500),
                    authenticatedServerId = authenticatedServerId,
                )
                if (!isCurrentLanScan(generation, scanGeneration.get(), started)) return@withLock
                if (localServer != null) {
                    consecutiveLanMisses = 0
                    if (shouldSwitchToLan(currentUrl, localServer.url)) {
                        sessionRepository.setBaseUrl(localServer.url)
                    }
                } else {
                    consecutiveLanMisses += 1
                    publicFallbackAfterLanLoss(
                        currentUrl = currentUrl,
                        publicUrl = DEFAULT_MULLETAFLIX_SERVER_URL,
                        consecutiveMisses = consecutiveLanMisses,
                    )?.let { fallbackUrl -> sessionRepository.setBaseUrl(fallbackUrl) }
                }
            }
        }
    }
}
