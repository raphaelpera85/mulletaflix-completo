package org.mulletaflix.android.service

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DownloadIdentityTest {
    @Test
    fun `scopes Media3 request ids by account`() {
        assertEquals("user-1::movie-1", scopedDownloadRequestId("user-1", "movie-1"))
        assertEquals("user-2::movie-1", scopedDownloadRequestId("user-2", "movie-1"))
    }

    @Test
    fun `only the owning account can see a download`() {
        assertTrue(downloadBelongsToUser("user-1", "user-1"))
        assertFalse(downloadBelongsToUser("user-1", "user-2"))
        assertFalse(downloadBelongsToUser(null, "user-1"))
    }

    @Test
    fun `recovers public item id from scoped request id`() {
        assertEquals("movie-1", publicDownloadItemId("user-1::movie-1", "user-1"))
        assertEquals("legacy-id", publicDownloadItemId("legacy-id", "user-1"))
    }
}
