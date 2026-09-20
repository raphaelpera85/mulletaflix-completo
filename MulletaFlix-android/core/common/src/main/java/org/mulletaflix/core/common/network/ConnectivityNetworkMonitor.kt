package org.mulletaflix.core.common.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.ConnectivityManager.NetworkCallback
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import androidx.core.content.getSystemService
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.conflate
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ConnectivityNetworkMonitor @Inject constructor(
    @param:ApplicationContext private val context: Context,
) : NetworkMonitor {

    override val isOnline: Flow<Boolean> = callbackFlow {
        val connectivityManager = context.getSystemService<ConnectivityManager>()
        if (connectivityManager == null) {
            trySend(false)
            close()
            return@callbackFlow
        }

        val callback = object : NetworkCallback() {
            private val networks = mutableSetOf<Network>()

            override fun onAvailable(network: Network) {
                networks += network
                trySend(true)
            }

            override fun onLost(network: Network) {
                networks -= network
                trySend(networks.isNotEmpty())
            }

            override fun onCapabilitiesChanged(
                network: Network,
                networkCapabilities: NetworkCapabilities,
            ) {
                // A MulletaFlix server can be reachable only inside the local LAN.
                // Requiring VALIDATED here incorrectly marks a Wi-Fi network without
                // internet access as offline and prevents LAN playback/recovery.
                val hasInternet = isUsableForServerAccess(
                    hasInternetCapability = networkCapabilities.hasCapability(
                        NetworkCapabilities.NET_CAPABILITY_INTERNET,
                    ),
                )
                if (hasInternet) {
                    networks += network
                } else {
                    networks -= network
                }
                trySend(networks.isNotEmpty())
            }
        }

        val request = NetworkRequest.Builder()
            .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
            .build()

        connectivityManager.registerNetworkCallback(request, callback)

        // Initial state check
        val currentNetwork = connectivityManager.activeNetwork
        val capabilities = connectivityManager.getNetworkCapabilities(currentNetwork)
        val isCurrentlyConnected = capabilities?.let {
            isUsableForServerAccess(
                hasInternetCapability = it.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET),
            )
        } == true
        trySend(isCurrentlyConnected)

        awaitClose {
            connectivityManager.unregisterNetworkCallback(callback)
        }
    }.conflate()
}

/**
 * `VALIDATED` means internet connectivity, not reachability of a local server.
 * The INTERNET capability is the Android signal that the link can carry IP traffic;
 * the actual MulletaFlix endpoint remains the source of truth for server reachability.
 */
internal fun isUsableForServerAccess(hasInternetCapability: Boolean): Boolean = hasInternetCapability
