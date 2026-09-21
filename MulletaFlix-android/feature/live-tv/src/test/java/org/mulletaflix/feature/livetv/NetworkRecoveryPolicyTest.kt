package org.mulletaflix.feature.livetv

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class NetworkRecoveryPolicyTest {
    @Test
    fun `refreshes live tv after connectivity returns`() {
        assertTrue(shouldRefreshLiveTvOnNetworkReturn(previousOnline = false, currentOnline = true))
    }

    @Test
    fun `does not refresh on initial or unchanged connectivity`() {
        assertFalse(shouldRefreshLiveTvOnNetworkReturn(previousOnline = null, currentOnline = true))
        assertFalse(shouldRefreshLiveTvOnNetworkReturn(previousOnline = true, currentOnline = true))
        assertFalse(shouldRefreshLiveTvOnNetworkReturn(previousOnline = false, currentOnline = false))
    }

    @Test
    fun `foreground timer refreshes only when the live tv request is idle`() {
        assertTrue(shouldRefreshLiveTvIfIdle(isOffline = false, isLoading = false))
        assertFalse(shouldRefreshLiveTvIfIdle(isOffline = true, isLoading = false))
        assertFalse(shouldRefreshLiveTvIfIdle(isOffline = false, isLoading = true))
    }
}
