package org.mulletaflix.feature.itemdetail

internal fun isBookSearchAvailable(
    hasPlainText: Boolean,
    hasEpubSearch: Boolean,
    hasPageTextSearch: Boolean,
): Boolean = hasPlainText || hasEpubSearch || hasPageTextSearch
