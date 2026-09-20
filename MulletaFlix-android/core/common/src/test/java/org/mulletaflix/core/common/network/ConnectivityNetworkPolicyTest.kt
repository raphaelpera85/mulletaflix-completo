package org.mulletaflix.core.common.network

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ConnectivityNetworkPolicyTest {

    @Test
    fun `keeps a local network usable without validated internet`() {
        assertTrue(isUsableForServerAccess(hasInternetCapability = true))
    }

    @Test
    fun `rejects networks that cannot carry server traffic`() {
        assertFalse(isUsableForServerAccess(hasInternetCapability = false))
    }
}
