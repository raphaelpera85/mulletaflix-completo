package org.mulletaflix.feature.library

/** Single source of truth for the infinite-scroll request guard. */
internal fun shouldRequestNextLibraryPage(
    isLoading: Boolean,
    hasMore: Boolean,
): Boolean = !isLoading && hasMore

/**
 * Whether another page should be requested after a catalog response.
 *
 * Both conditions are required:
 *
 * - the accumulated list is still smaller than the total the server reported;
 * - the response actually carried items.
 *
 * The second condition matters because otherwise an empty page against a stale
 * or optimistic `total` keeps the flag true forever: the grid keeps its loading
 * sentinel visible and every recomposition of that sentinel asks for the same
 * `startIndex` again.
 */
internal fun hasMoreLibraryPages(
    loadedItemCount: Int,
    receivedItemCount: Int,
    totalItemCount: Int,
): Boolean = receivedItemCount > 0 && loadedItemCount < totalItemCount
