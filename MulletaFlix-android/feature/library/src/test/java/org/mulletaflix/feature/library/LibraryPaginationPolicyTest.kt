package org.mulletaflix.feature.library

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/** Documents the guard used by the library's infinite-scroll sentinel. */
class LibraryPaginationPolicyTest {

    @Test
    fun `does not accept a second request while a page is loading`() {
        assertTrue(shouldRequestNextLibraryPage(isLoading = false, hasMore = true))
        assertFalse(shouldRequestNextLibraryPage(isLoading = true, hasMore = true))
    }

    @Test
    fun `does not request after the last page`() {
        assertFalse(shouldRequestNextLibraryPage(isLoading = false, hasMore = false))
    }

    @Test
    fun `keeps paging while the server reports more items`() {
        assertTrue(
            hasMoreLibraryPages(
                loadedItemCount = 20,
                receivedItemCount = 20,
                totalItemCount = 100,
            ),
        )
    }

    @Test
    fun `stops paging when the reported total is reached`() {
        assertFalse(
            hasMoreLibraryPages(
                loadedItemCount = 100,
                receivedItemCount = 20,
                totalItemCount = 100,
            ),
        )
    }

    @Test
    fun `an empty page stops paging even when the total is larger`() {
        // A stale or optimistic total used to keep hasMore true forever: the
        // grid kept its loading sentinel and re-requested the same offset.
        assertFalse(
            hasMoreLibraryPages(
                loadedItemCount = 20,
                receivedItemCount = 0,
                totalItemCount = 100,
            ),
        )
    }

    @Test
    fun `an empty first page leaves nothing to paginate`() {
        assertFalse(
            hasMoreLibraryPages(
                loadedItemCount = 0,
                receivedItemCount = 0,
                totalItemCount = 50,
            ),
        )
    }
}
