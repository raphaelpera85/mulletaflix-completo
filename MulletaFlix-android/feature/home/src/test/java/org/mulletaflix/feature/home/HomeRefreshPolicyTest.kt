package org.mulletaflix.feature.home

import org.junit.Assert.assertEquals
import org.junit.Test

class HomeRefreshPolicyTest {
    @Test
    fun `television refreshes the home feed once per minute`() {
        assertEquals(
            TV_HOME_REFRESH_INTERVAL_MILLIS,
            homeAutoRefreshIntervalMillis(isTelevision = true),
        )
    }

    @Test
    fun `phone and tablet keep explicit refresh behavior`() {
        assertEquals(0L, homeAutoRefreshIntervalMillis(isTelevision = false))
    }

    @Test
    fun `television refreshes immediately when returning to foreground`() {
        assertEquals(true, refreshHomeImmediatelyOnResume(isTelevision = true))
        assertEquals(false, refreshHomeImmediatelyOnResume(isTelevision = false))
    }
}
