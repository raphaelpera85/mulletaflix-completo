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

    @Test
    fun `epg refreshes only while its dialog is open`() {
        assertTrue(shouldRefreshLiveTvGuide(isGuideOpen = true))
        assertEquals(false, shouldRefreshLiveTvGuide(isGuideOpen = false))
        assertEquals(60_000L, LIVE_TV_GUIDE_REFRESH_INTERVAL_MILLIS)
    }
}
