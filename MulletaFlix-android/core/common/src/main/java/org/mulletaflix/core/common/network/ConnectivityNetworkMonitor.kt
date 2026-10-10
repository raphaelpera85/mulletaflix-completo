package org.mulletaflix.core.common.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.ConnectivityManager.NetworkCallback
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import android.os.Build
import android.os.Handler
import android.os.Looper
import androidx.core.content.getSystemService
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.conflate
import javax.inject.Inject
import javax.inject.Singleton

internal class ServerAccessNetworkCallback(
    private val scheduleLegacyCapabilityLookup: (Network) -> Unit,
    private val sdkInt: Int,
    private val onNetworkStateChanged: (Boolean) -> Unit,
) : NetworkCallback() {
    private val networks = mutableSetOf<Network>()

    override fun onAvailable(network: Network) {
        if (requiresLegacyCapabilityLookup(sdkInt)) {
            scheduleLegacyCapabilityLookup(network)
        }
    }

    override fun onLost(network: Network) {
        networks -= network
        onNetworkStateChanged(networks.isNotEmpty())
    }

    override fun onCapabilitiesChanged(network: Network, networkCapabilities: NetworkCapabilities) {
        updateNetwork(network, networkCapabilities)
    }

    fun onLegacyCapabilitiesAvailable(network: Network, capabilities: NetworkCapabilities?) {
        capabilities?.let { updateNetwork(network, it) }
    }

    private fun updateNetwork(network: Network, capabilities: NetworkCapabilities) {
        // Local-only Wi-Fi/Ethernet can still reach a media server. Request
        // callbacks for trusted networks generally, then distinguish usable
        // internet routes from local transports using their capabilities.
        val usable = isUsableForServerAccess(
            hasInternetCapability = capabilities.hasCapability(
                NetworkCapabilities.NET_CAPABILITY_INTERNET,
            ),
            hasWifiTransport = capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI),
            hasEthernetTransport = capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET),
        )
        if (usable) networks += network else networks -= network
        onNetworkStateChanged(networks.isNotEmpty())
    }
}

internal class ActiveNetworkMeteredCallback(
    private val scheduleLegacyMeteredLookup: (Network) -> Unit,
    private val sdkInt: Int,
    private val onMeteredStateChanged: (Boolean) -> Unit,
) : NetworkCallback() {
    override fun onAvailable(network: Network) {
        if (requiresLegacyCapabilityLookup(sdkInt)) scheduleLegacyMeteredLookup(network)
    }

    override fun onLost(network: Network) = onMeteredStateChanged(false)

    fun onLegacyMeteredStateAvailable(isMetered: Boolean) = onMeteredStateChanged(isMetered)

    override fun onCapabilitiesChanged(network: Network, networkCapabilities: NetworkCapabilities) =
        onMeteredStateChanged(
            !networkCapabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_METERED),
        )
}

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

        lateinit var callback: ServerAccessNetworkCallback
        callback = ServerAccessNetworkCallback(
            scheduleLegacyCapabilityLookup = { network ->
                Handler(Looper.getMainLooper()).post {
                    if (connectivityManager.activeNetwork == network) {
                        callback.onLegacyCapabilitiesAvailable(
                            network,
                            connectivityManager.getNetworkCapabilities(network),
                        )
                    }
                }
            },
            sdkInt = Build.VERSION.SDK_INT,
            onNetworkStateChanged = { trySend(it) },
        )

        val request = serverAccessNetworkRequest()

        connectivityManager.registerNetworkCallback(request, callback)

        // Initial state check
        val currentNetwork = connectivityManager.activeNetwork
        val capabilities = connectivityManager.getNetworkCapabilities(currentNetwork)
        val isCurrentlyConnected = capabilities?.let {
            isUsableForServerAccess(
                hasInternetCapability = it.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET),
                hasWifiTransport = it.hasTransport(NetworkCapabilities.TRANSPORT_WIFI),
                hasEthernetTransport = it.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET),
            )
        } == true
        trySend(isCurrentlyConnected)

        awaitClose {
            connectivityManager.unregisterNetworkCallback(callback)
        }
    }.conflate()

    override val isMetered: Flow<Boolean> = callbackFlow {
        val connectivityManager = context.getSystemService<ConnectivityManager>()
        if (connectivityManager == null) {
            trySend(false)
            close()
            return@callbackFlow
        }

        lateinit var callback: ActiveNetworkMeteredCallback
        callback = ActiveNetworkMeteredCallback(
            scheduleLegacyMeteredLookup = { network ->
                Handler(Looper.getMainLooper()).post {
                    if (connectivityManager.activeNetwork == network) {
                        callback.onLegacyMeteredStateAvailable(connectivityManager.isActiveNetworkMetered)
                    }
                }
            },
            sdkInt = Build.VERSION.SDK_INT,
            onMeteredStateChanged = { trySend(it) },
        )
        trySend(connectivityManager.isActiveNetworkMetered)
        connectivityManager.registerDefaultNetworkCallback(callback)
        awaitClose { connectivityManager.unregisterNetworkCallback(callback) }
    }.conflate()
}

/** Observe trusted networks even when Android does not mark them as internet-capable. */
fun serverAccessNetworkRequest(): NetworkRequest = NetworkRequest.Builder().build()

/** Internet routes work remotely; Wi-Fi/Ethernet can also reach a server on an isolated LAN. */
internal fun isUsableForServerAccess(
    hasInternetCapability: Boolean,
    hasWifiTransport: Boolean = false,
    hasEthernetTransport: Boolean = false,
): Boolean = hasInternetCapability || hasWifiTransport || hasEthernetTransport

internal fun isMeteredNetwork(isActiveNetworkMetered: Boolean): Boolean = isActiveNetworkMetered

internal fun requiresLegacyCapabilityLookup(sdkInt: Int): Boolean = sdkInt < Build.VERSION_CODES.O
