package org.mulletaflix.core.common.network

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ConnectivityNetworkPolicyTest {

    @Test
    fun `keeps wifi and ethernet usable for local servers without internet capability`() {
        assertTrue(
            isUsableForServerAccess(
                hasInternetCapability = false,
                hasWifiTransport = true,
            ),
        )
        assertTrue(
            isUsableForServerAccess(
                hasInternetCapability = false,
                hasEthernetTransport = true,
            ),
        )
    }

    @Test
    fun `keeps internet capable networks usable`() {
        assertTrue(isUsableForServerAccess(hasInternetCapability = true))
    }

    @Test
    fun `rejects networks without internet or local server transport`() {
        assertFalse(
            isUsableForServerAccess(
                hasInternetCapability = false,
                hasWifiTransport = false,
                hasEthernetTransport = false,
            ),
        )
    }

    @Test
    fun `uses the Android active transport metered signal as the source of truth`() {
        assertTrue(isMeteredNetwork(isActiveNetworkMetered = true))
        assertFalse(isMeteredNetwork(isActiveNetworkMetered = false))
    }

    @Test
    fun `uses deferred capabilities lookup only before Android O callback ordering guarantee`() {
        assertTrue(requiresLegacyCapabilityLookup(sdkInt = 24))
        assertTrue(requiresLegacyCapabilityLookup(sdkInt = 25))
        assertFalse(requiresLegacyCapabilityLookup(sdkInt = 26))
        assertFalse(requiresLegacyCapabilityLookup(sdkInt = 35))
    }
}
