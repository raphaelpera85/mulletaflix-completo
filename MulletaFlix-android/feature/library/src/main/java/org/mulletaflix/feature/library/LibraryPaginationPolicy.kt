package org.mulletaflix.feature.library

/** Single source of truth for the infinite-scroll request guard. */
internal fun shouldRequestNextLibraryPage(
    isLoading: Boolean,
    hasMore: Boolean,
): Boolean = !isLoading && hasMore
