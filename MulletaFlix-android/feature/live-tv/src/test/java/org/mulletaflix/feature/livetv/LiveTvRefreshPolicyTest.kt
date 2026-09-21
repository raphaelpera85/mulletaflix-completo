package org.mulletaflix.feature.livetv

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LiveTvRefreshPolicyTest {
    @Test
    fun `television refreshes live channels immediately and every minute`() {
        assertTrue(refreshLiveTvImmediatelyOnResume(isTelevision = true))
        assertEquals(TV_LIVE_REFRESH_INTERVAL_MILLIS, liveTvAutoRefreshIntervalMillis(isTelevision = true))
    }

    @Test
    fun `handheld devices keep manual live tv refresh`() {
        assertEquals(0L, liveTvAutoRefreshIntervalMillis(isTelevision = false))
        assertEquals(false, refreshLiveTvImmediatelyOnResume(isTelevision = false))
    }
}
