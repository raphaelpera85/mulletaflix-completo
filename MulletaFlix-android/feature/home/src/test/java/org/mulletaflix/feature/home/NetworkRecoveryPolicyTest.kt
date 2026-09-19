package org.mulletaflix.feature.home

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class NetworkRecoveryPolicyTest {

    @Test
    fun `refreshes when an offline session becomes online`() {
        assertTrue(shouldRefreshHomeOnNetworkReturn(previousOnline = false, currentOnline = true))
    }

    @Test
    fun `does not refresh on initial or unchanged online states`() {
        assertFalse(shouldRefreshHomeOnNetworkReturn(previousOnline = null, currentOnline = true))
        assertFalse(shouldRefreshHomeOnNetworkReturn(previousOnline = true, currentOnline = true))
        assertFalse(shouldRefreshHomeOnNetworkReturn(previousOnline = false, currentOnline = false))
    }
}
