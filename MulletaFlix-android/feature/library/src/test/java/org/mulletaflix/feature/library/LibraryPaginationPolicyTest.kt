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
}
