package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LibraryRefreshPolicyTest {
    @Test
    fun `refreshes once when an offline library becomes online`() {
        assertTrue(shouldRefreshLibraryOnNetworkReturn(previousOnline = false, currentOnline = true))
    }

    @Test
    fun `does not refresh on initial or unchanged network states`() {
        assertEquals(false, shouldRefreshLibraryOnNetworkReturn(previousOnline = null, currentOnline = true))
        assertEquals(false, shouldRefreshLibraryOnNetworkReturn(previousOnline = true, currentOnline = true))
        assertEquals(false, shouldRefreshLibraryOnNetworkReturn(previousOnline = false, currentOnline = false))
    }

    @Test
    fun `television refreshes immediately and periodically while visible`() {
        assertTrue(libraryRefreshImmediatelyOnResume(isTelevision = true))
        assertEquals(60_000L, libraryAutoRefreshIntervalMillis(isTelevision = true))
    }

    @Test
    fun `phone and tablet keep explicit refresh behavior`() {
        assertEquals(false, libraryRefreshImmediatelyOnResume(isTelevision = false))
        assertEquals(0L, libraryAutoRefreshIntervalMillis(isTelevision = false))
    }
}
