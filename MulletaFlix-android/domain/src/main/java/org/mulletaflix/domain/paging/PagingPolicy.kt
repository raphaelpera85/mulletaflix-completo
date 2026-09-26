package org.mulletaflix.domain.paging

/**
 * Paging rules shared by every "load more as you scroll" list in the app.
 *
 * They lived in `:feature:library` only, which meant Live TV could not reuse them
 * and was about to grow a second copy. Both halves encode a subtlety that was a
 * real bug once, so the rule belongs in one place:
 *
 *  - [shouldRequestNextPage] closes the window where Compose asks for the
 *    sentinel item more than once during a fast scroll;
 *  - [hasMorePages] requires the response to have carried items, because an empty
 *    page against a stale or optimistic total otherwise keeps the flag true
 *    forever and the list re-requests the same offset on every recomposition;
 *  - [supportsOffsetPaging] and [singleRequestItemLimit] keep "Aleatório" out of
 *    offset paging, which that ordering cannot survive;
 *  - [appendDistinctBy] keeps a shifted offset window from rendering the same item
 *    twice.
 */

/** Whether a "next page" request should start now. */
fun shouldRequestNextPage(isLoading: Boolean, hasMore: Boolean): Boolean = !isLoading && hasMore

/**
 * Whether another page still exists after a response.
 *
 * Both conditions are required: the accumulated list is smaller than the total the
 * server reported, **and** the response actually carried items.
 */
fun hasMorePages(
    loadedItemCount: Int,
    receivedItemCount: Int,
    totalItemCount: Int,
): Boolean = receivedItemCount > 0 && loadedItemCount < totalItemCount

/** The sort the server answers with `EF.Functions.Random()`; see [supportsOffsetPaging]. */
const val RANDOM_SORT_API_VALUE = "Random"

/**
 * Whether this ordering can be paged by offset at all.
 *
 * `SortBy=Random` is translated by the server to `ORDER BY RANDOM()`
 * (`Jellyfin.Server.Implementations/Item/OrderMapper.cs`), which draws a **new**
 * permutation for every request. `Skip(offset)` then cuts a different shuffle each
 * time, so page 2 can repeat items from page 1 and omit others entirely, and the
 * "loaded < total" test ends the list before the catalogue has been shown. Offsets
 * and a random order are not compatible; no amount of retrying fixes it.
 *
 * Every other ordering is over columns that do not change between two requests, so
 * offset paging is coherent.
 */
fun supportsOffsetPaging(sortByApiValue: String): Boolean =
    !sortByApiValue.equals(RANDOM_SORT_API_VALUE, ignoreCase = true)

/**
 * How many items a single request must ask for.
 *
 * For a pageable ordering this is just [pageSize]: the list grows as the user
 * scrolls. For an ordering that cannot be paged it has to be the whole list, because
 * "the next page" does not exist in a permutation that is redrawn per request.
 *
 * `totalItemCount` is the total the first response reported; a null means it is not
 * known yet and only the page size can be asked for. The result is capped at
 * [maxSingleRequest] so a very large library cannot turn one screen into a
 * multi-thousand-item allocation.
 */
fun singleRequestItemLimit(
    sortByApiValue: String,
    pageSize: Int,
    totalItemCount: Int?,
    maxSingleRequest: Int = MAX_SINGLE_REQUEST_ITEMS,
): Int = if (supportsOffsetPaging(sortByApiValue)) {
    pageSize
} else {
    minOf(maxOf(pageSize, totalItemCount ?: pageSize), maxSingleRequest)
}

/**
 * Ceiling for a single request made to an ordering that cannot be paged.
 *
 * A cap is a deliberate compromise: past it the random view shows a random [cap]
 * instead of the whole library. The alternative — paginating a redrawn permutation —
 * is what produced repeated titles and missing ones.
 */
const val MAX_SINGLE_REQUEST_ITEMS = 1000

/**
 * Appends [incoming] to [existing], dropping entries whose [id] is already present.
 *
 * Two requests against an offset window are only disjoint if nothing changed on the
 * server in between. A library scan that inserts an item at the top of a
 * "date added" ordering shifts the whole window, so the tail of page N comes back as
 * the head of page N+1. The list is rendered with `key = item.id`, so appending the
 * duplicate gives two Composables the same identity.
 *
 * The order of [existing] is preserved and the first occurrence wins.
 */
fun <T> appendDistinctBy(existing: List<T>, incoming: List<T>, id: (T) -> String): List<T> {
    if (incoming.isEmpty()) return existing
    val seen = existing.mapTo(HashSet(existing.size)) { id(it) }
    return existing + incoming.filter { seen.add(id(it)) }
}
