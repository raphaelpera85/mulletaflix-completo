package org.mulletaflix.core.common.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ConnectivityNetworkCallbackTest {
    @Test
    fun availableWaitsForCapabilitiesCallbackBeforePublishingNetworkState() {
        val states = mutableListOf<Boolean>()
        var legacyLookupScheduled = false
        val callback = ServerAccessNetworkCallback(
            scheduleLegacyCapabilityLookup = { legacyLookupScheduled = true },
            sdkInt = 35,
            onNetworkStateChanged = states::add,
        )
        val context = ApplicationProvider.getApplicationContext<Context>()
        val network = context.getSystemService(ConnectivityManager::class.java).activeNetwork
            ?: error("Emulator must expose its active test network")

        callback.onAvailable(network)
        assertEquals(false, legacyLookupScheduled)
        assertTrue(states.isEmpty())

        val capabilities = context.getSystemService(ConnectivityManager::class.java)
            .getNetworkCapabilities(network)
            ?: error("Emulator active network must expose capabilities")
        callback.onCapabilitiesChanged(network, capabilities)
        assertEquals(
            isUsableForServerAccess(
                hasInternetCapability = capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET),
                hasWifiTransport = capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI),
                hasEthernetTransport = capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET),
            ),
            states.last(),
        )
    }

    @Test
    fun meteredCallbackDerivesStateFromDeliveredCapabilitiesAndClearsOnLoss() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        val connectivityManager = context.getSystemService(ConnectivityManager::class.java)
        val network = connectivityManager.activeNetwork
            ?: error("Emulator must expose its active test network")
        val capabilities = connectivityManager.getNetworkCapabilities(network)
            ?: error("Emulator active network must expose capabilities")
        val states = mutableListOf<Boolean>()
        val callback = ActiveNetworkMeteredCallback(
            scheduleLegacyMeteredLookup = {},
            sdkInt = 35,
            onMeteredStateChanged = states::add,
        )

        callback.onAvailable(network)
        assertTrue(states.isEmpty())
        callback.onCapabilitiesChanged(network, capabilities)
        assertEquals(
            !capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_METERED),
            states.last(),
        )
        callback.onLost(network)
        assertEquals(false, states.last())
    }
}
