package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LibraryFacetFiltersTest {
    @Test
    fun `normalizes multi value fields to the server separators`() {
        assertEquals(
            LibraryFacetFilters(
                genres = "Drama|Ação",
                years = "2023,2024",
                officialRatings = "PG-13|TV-MA",
            ),
            normalizeLibraryFacetFilters(
                LibraryFacetFilters(" Drama, Ação,Drama ", "2023; 2024", "PG-13, TV-MA"),
            ),
        )
    }

    @Test
    fun `rejects years that are not exactly four digits`() {
        assertNull(normalizeLibraryFacetFilters(LibraryFacetFilters(years = "2024,20xx")))
        assertFalse(isValidLibraryYearInput("2024, 24"))
        assertTrue(isValidLibraryYearInput(" , 2024, 2025 "))
    }

    @Test
    fun `persisted facets can be edited and rendered as readable chips`() {
        val selected = activeFiltersWithFacets(
            basicFilters = listOf(LibraryViewModel.FILTER_FAVORITES),
            facets = requireNotNull(
                normalizeLibraryFacetFilters(LibraryFacetFilters("Drama,Ação", "2024,2025", "PG-13,TV-MA")),
            ),
        )

        assertEquals(
            LibraryFacetFilters("Drama, Ação", "2024, 2025", "PG-13, TV-MA"),
            facetFiltersFromActive(selected),
        )
        assertEquals("Gêneros: Drama,Ação", libraryFacetFilterLabel("library_genres:Drama|Ação"))
        assertEquals(
            listOf("library_genres:Drama|Ação", "library_years:2024,2025", "library_ratings:PG-13|TV-MA"),
            libraryFacetFiltersInServerOrder(selected),
        )
    }
}
