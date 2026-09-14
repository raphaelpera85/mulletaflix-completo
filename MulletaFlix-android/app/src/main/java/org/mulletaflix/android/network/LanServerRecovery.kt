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
import javax.inject.Inject
import javax.inject.Singleton

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
    private var registered = false

    private val callback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) {
            scheduleScan()
        }
    }

    fun start() {
        if (registered) return
        val callbackRegistered = runCatching {
            connectivityManager.registerNetworkCallback(
                NetworkRequest.Builder()
                    .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                    .build(),
                callback,
            )
            true
        }.getOrDefault(false)
        if (callbackRegistered) {
            registered = true
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
    }

    fun stop() {
        if (!registered) return
        registered = false
        runCatching { connectivityManager.unregisterNetworkCallback(callback) }
        scanJob?.cancel()
        scanJob = null
        sessionJob?.cancel()
        sessionJob = null
    }

    private fun scheduleScan() {
        scanJob?.cancel()
        scanJob = scope.launch {
            scanMutex.withLock {
                val userId = sessionRepository.getCurrentUserId().first() ?: return@withLock
                val currentUrl = sessionRepository.getBaseUrl().first()
                val localServer = discovery.discover(timeoutMs = 2_500).firstOrNull() ?: return@withLock
                if (shouldSwitchToLan(currentUrl, localServer.url)) {
                    sessionRepository.setBaseUrl(localServer.url)
                }
            }
        }
    }
}
