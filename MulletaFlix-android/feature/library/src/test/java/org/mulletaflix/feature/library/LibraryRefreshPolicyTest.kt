package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LibraryRefreshPolicyTest {
    @Test
    fun `library refreshes immediately and periodically while visible`() {
        assertTrue(libraryRefreshImmediatelyOnResume())
        assertEquals(60_000L, LIBRARY_AUTO_REFRESH_INTERVAL_MILLIS)
    }
}
