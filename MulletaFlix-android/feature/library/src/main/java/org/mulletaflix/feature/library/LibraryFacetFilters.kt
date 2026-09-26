package org.mulletaflix.feature.library

/** User-entered values for the server-side library facets. */
data class LibraryFacetFilters(
    val genres: String = "",
    val years: String = "",
    val officialRatings: String = "",
)

private const val GENRES_PREFIX = "library_genres:"
private const val YEARS_PREFIX = "library_years:"
private const val RATINGS_PREFIX = "library_ratings:"
private val INPUT_SEPARATOR = Regex("[,;|]")
private val YEAR_VALUE = Regex("[0-9]{4}")

internal fun normalizeLibraryFacetFilters(input: LibraryFacetFilters): LibraryFacetFilters? {
    val years = values(input.years)
    if (years.any { !YEAR_VALUE.matches(it) }) return null
    return LibraryFacetFilters(
        genres = values(input.genres).joinToString("|"),
        years = years.joinToString(","),
        officialRatings = values(input.officialRatings).joinToString("|"),
    )
}

internal fun isValidLibraryYearInput(input: String): Boolean =
    values(input).all { YEAR_VALUE.matches(it) }

internal fun facetFiltersFromActive(activeFilters: Collection<String>): LibraryFacetFilters =
    LibraryFacetFilters(
        genres = activeFilters.firstValue(GENRES_PREFIX).serverValuesToInput('|'),
        years = activeFilters.firstValue(YEARS_PREFIX).serverValuesToInput(','),
        officialRatings = activeFilters.firstValue(RATINGS_PREFIX).serverValuesToInput('|'),
    )

internal fun facetFiltersForRequest(activeFilters: Collection<String>): LibraryFacetFilters =
    LibraryFacetFilters(
        genres = activeFilters.firstValue(GENRES_PREFIX),
        years = activeFilters.firstValue(YEARS_PREFIX),
        officialRatings = activeFilters.firstValue(RATINGS_PREFIX),
    )

internal fun activeFiltersWithFacets(
    basicFilters: Collection<String>,
    facets: LibraryFacetFilters,
): List<String> = buildList {
    addAll(basicFilters.filter { it in LibraryViewModel.BASIC_FILTERS })
    facets.genres.takeIf(String::isNotBlank)?.let { add("$GENRES_PREFIX$it") }
    facets.years.takeIf(String::isNotBlank)?.let { add("$YEARS_PREFIX$it") }
    facets.officialRatings.takeIf(String::isNotBlank)?.let { add("$RATINGS_PREFIX$it") }
}

internal fun isLibraryFacetFilter(filter: String): Boolean =
    filter.startsWith(GENRES_PREFIX) || filter.startsWith(YEARS_PREFIX) || filter.startsWith(RATINGS_PREFIX)

internal fun libraryFacetFilterLabel(filter: String): String = when {
    filter.startsWith(GENRES_PREFIX) -> "Gêneros: ${filter.removePrefix(GENRES_PREFIX).replace('|', ',')}"
    filter.startsWith(YEARS_PREFIX) -> "Anos: ${filter.removePrefix(YEARS_PREFIX).split(',').joinToString(", ")}"
    filter.startsWith(RATINGS_PREFIX) -> "Classificação: ${filter.removePrefix(RATINGS_PREFIX).replace('|', ',')}"
    else -> filter
}

internal fun libraryFacetFiltersInServerOrder(filters: Collection<String>): List<String> = buildList {
    filters.firstValue(GENRES_PREFIX).takeIf(String::isNotBlank)?.let { add("$GENRES_PREFIX$it") }
    filters.firstValue(YEARS_PREFIX).takeIf(String::isNotBlank)?.let { add("$YEARS_PREFIX$it") }
    filters.firstValue(RATINGS_PREFIX).takeIf(String::isNotBlank)?.let { add("$RATINGS_PREFIX$it") }
}

private fun Collection<String>.firstValue(prefix: String): String =
    firstOrNull { it.startsWith(prefix) }?.removePrefix(prefix).orEmpty()

private fun String.serverValuesToInput(separator: Char): String =
    split(separator).map(String::trim).filter(String::isNotEmpty).joinToString(", ")

private fun values(input: String): List<String> =
    input.split(INPUT_SEPARATOR).map(String::trim).filter(String::isNotEmpty).distinct()
