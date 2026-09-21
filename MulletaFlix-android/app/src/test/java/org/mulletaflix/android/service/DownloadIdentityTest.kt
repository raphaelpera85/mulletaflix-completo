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

    @Test
    fun `a media id survives a round trip through the account scope`() {
        val itemId = "episode-202"
        val scoped = scopedDownloadRequestId("user-1", itemId)
        assertEquals(itemId, publicDownloadItemId(scoped, "user-1"))
    }

    @Test
    fun `the scope contract trims the account id on both sides`() {
        // `scopedDownloadRequestId` trims. If `publicDownloadItemId` did not, an
        // account id carrying whitespace would leave the prefix in place and
        // return the whole scoped id as if it were a media id.
        val scoped = scopedDownloadRequestId(" user-1 ", "movie-1")
        assertEquals("user-1::movie-1", scoped)
        assertEquals("movie-1", publicDownloadItemId(scoped, " user-1 "))
        assertEquals("movie-1", publicDownloadItemId(scoped, "user-1"))
    }

    @Test
    fun `another account never resolves someone else's scoped id`() {
        // Must not strip a prefix belonging to a different account: the caller
        // then sees the raw scoped id instead of a wrong media id.
        assertEquals(
            "user-2::movie-1",
            publicDownloadItemId("user-2::movie-1", "user-1"),
        )
    }

    @Test
    fun `an unscoped entry with no owner is recognised as legacy`() {
        // Releases up to 1.0.6 stored the raw media id and wrote no owner. Such
        // an entry is otherwise filtered out forever: invisible, unremovable and
        // unreclaimable, so it has to be adopted.
        assertTrue(isLegacyUnscopedDownload("movie-1", ownerUserId = null))
        assertTrue(isLegacyUnscopedDownload("movie-1", ownerUserId = ""))
        assertTrue(isLegacyUnscopedDownload("episode 202", ownerUserId = "  "))
    }

    @Test
    fun `a scoped entry is never treated as legacy`() {
        assertFalse(isLegacyUnscopedDownload("user-1::movie-1", ownerUserId = null))
        assertFalse(isLegacyUnscopedDownload("user-2::movie-1", ownerUserId = "user-1"))
    }

    @Test
    fun `an entry that already has an owner is never treated as legacy`() {
        assertFalse(isLegacyUnscopedDownload("movie-1", ownerUserId = "user-1"))
    }

    @Test
    fun `the adopted id is the media id, not the scoped one`() {
        // Adoption declares the legacy request id to be the media id, which is
        // exactly what the unscoped format meant.
        val legacyRequestId = "movie-1"
        assertTrue(isLegacyUnscopedDownload(legacyRequestId, null))
        assertEquals(legacyRequestId, publicDownloadItemId(legacyRequestId, "user-1"))
    }
}
