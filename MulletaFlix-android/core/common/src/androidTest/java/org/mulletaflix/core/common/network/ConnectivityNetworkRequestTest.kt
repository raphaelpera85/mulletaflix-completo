package org.mulletaflix.core.common.network

import android.net.NetworkCapabilities
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertFalse
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ConnectivityNetworkRequestTest {

    @Test
    fun localServerMonitoringDoesNotRequireInternetCapability() {
        val request = serverAccessNetworkRequest()

        assertFalse(
            "LAN discovery and playback must observe isolated Wi-Fi/Ethernet networks",
            request.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET),
        )
    }
}
