package org.mulletaflix.domain.model

/** Distinct genre, year, and rating values exposed by a media library. */
data class LibraryFilterOptions(
    val genres: List<String> = emptyList(),
    val years: List<Int> = emptyList(),
    val officialRatings: List<String> = emptyList(),
)
