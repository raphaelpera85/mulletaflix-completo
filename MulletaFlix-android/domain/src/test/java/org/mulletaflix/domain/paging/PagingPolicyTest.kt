package org.mulletaflix.domain.paging

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Documents the guard every "load more as you scroll" list uses — currently the
 * library grid, Minha Lista and the Live TV channel list.
 */
class PagingPolicyTest {

    @Test
    fun `does not accept a second request while a page is loading`() {
        assertTrue(shouldRequestNextPage(isLoading = false, hasMore = true))
        assertFalse(shouldRequestNextPage(isLoading = true, hasMore = true))
    }

    @Test
    fun `does not request after the last page`() {
        assertFalse(shouldRequestNextPage(isLoading = false, hasMore = false))
    }

    @Test
    fun `keeps paging while the server reports more items`() {
        assertTrue(
            hasMorePages(
                loadedItemCount = 20,
                receivedItemCount = 20,
                totalItemCount = 100,
            ),
        )
    }

    @Test
    fun `stops paging when the reported total is reached`() {
        assertFalse(
            hasMorePages(
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
            hasMorePages(
                loadedItemCount = 20,
                receivedItemCount = 0,
                totalItemCount = 100,
            ),
        )
    }

    @Test
    fun `an empty first page leaves nothing to paginate`() {
        assertFalse(
            hasMorePages(
                loadedItemCount = 0,
                receivedItemCount = 0,
                totalItemCount = 50,
            ),
        )
    }

    @Test
    fun `a random ordering cannot be paged by offset`() {
        // The server answers `SortBy=Random` with `ORDER BY RANDOM()`, so the window
        // at offset 40 of the *next* request is a different shuffle: the list gains
        // repeats and loses titles that were never shown.
        assertFalse(supportsOffsetPaging(RANDOM_SORT_API_VALUE))
        assertFalse(supportsOffsetPaging("random"))
        assertFalse(supportsOffsetPaging("RANDOM"))
    }

    @Test
    fun `every ordered sort is still paged by offset`() {
        listOf("SortName", "DateCreated", "PremiereDate", "Runtime", "CommunityRating")
            .forEach { assertTrue("$it should be pageable", supportsOffsetPaging(it)) }
    }

    @Test
    fun `an unpaginatable ordering asks for the whole list in one request`() {
        // 120 films, a 40-item page: page 1 would report 120 and the offset request
        // for page 2 would be meaningless, so the single request has to carry all 120.
        assertEquals(
            120,
            singleRequestItemLimit(
                sortByApiValue = RANDOM_SORT_API_VALUE,
                pageSize = 40,
                totalItemCount = 120,
            ),
        )
    }

    @Test
    fun `a pageable ordering still asks for one page`() {
        assertEquals(
            40,
            singleRequestItemLimit(
                sortByApiValue = "SortName",
                pageSize = 40,
                totalItemCount = 120,
            ),
        )
    }

    @Test
    fun `an unknown total keeps the single request at the page size`() {
        assertEquals(
            40,
            singleRequestItemLimit(
                sortByApiValue = RANDOM_SORT_API_VALUE,
                pageSize = 40,
                totalItemCount = null,
            ),
        )
    }

    @Test
    fun `a huge random library is capped so one screen cannot allocate unbounded`() {
        assertEquals(
            MAX_SINGLE_REQUEST_ITEMS,
            singleRequestItemLimit(
                sortByApiValue = RANDOM_SORT_API_VALUE,
                pageSize = 40,
                totalItemCount = 500_000,
            ),
        )
    }

    @Test
    fun `appending a page drops what the list already shows`() {
        // A library scan that inserts an item at the top of a "date added" ordering
        // shifts the window: the tail of page 1 comes back as the head of page 2.
        val page1 = listOf("a", "b", "c", "d")
        val page2 = listOf("c", "d", "e", "f")

        assertEquals(
            listOf("a", "b", "c", "d", "e", "f"),
            appendDistinctBy(page1, page2) { it },
        )
    }

    @Test
    fun `appending an entirely repeated page changes nothing`() {
        val page1 = listOf("a", "b")
        assertEquals(page1, appendDistinctBy(page1, listOf("a", "b")) { it })
    }

    @Test
    fun `appending an empty page keeps the same list instance`() {
        val page1 = listOf("a", "b")
        val appended = appendDistinctBy(page1, emptyList()) { it }
        assertTrue("an empty page must not rebuild the list", appended === page1)
    }
}
