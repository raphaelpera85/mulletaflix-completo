package org.mulletaflix.feature.auth

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LocalNetworkPermissionPolicyTest {
    @Test
    fun `requires permission only when both runtime and target are Android 17`() {
        assertFalse(requiresLocalNetworkPermission(36, 37, permissionGranted = false))
        assertFalse(requiresLocalNetworkPermission(37, 36, permissionGranted = false))
        assertTrue(requiresLocalNetworkPermission(37, 37, permissionGranted = false))
        assertFalse(requiresLocalNetworkPermission(37, 37, permissionGranted = true))
    }
}
