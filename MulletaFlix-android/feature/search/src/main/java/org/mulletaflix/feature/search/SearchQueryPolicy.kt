package org.mulletaflix.feature.search

/** Canonicalizes user-entered queries before they reach history or a search job. */
internal fun normalizeSearchQuery(query: String): String? =
    query.trim().takeIf(String::isNotEmpty)
