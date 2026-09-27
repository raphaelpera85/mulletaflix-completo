package org.mulletaflix.feature.search

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.SearchHintItem

class SearchContentPolicyTest {
    @Test
    fun `television hides books from results and hints but touch devices keep them`() {
        val results = listOf(
            MediaItem("movie", "Filme", MediaItemType.Movie),
            MediaItem("book", "Livro", MediaItemType.Book),
        )
        val hints = listOf(
            SearchHintItem("movie", "Filme", "Movie", 2020, null),
            SearchHintItem("book", "Livro", "Book", 2020, null),
        )

        assertEquals(listOf("movie"), searchItemsForDevice(results, isTelevision = true).map { it.id })
        assertEquals(listOf("movie"), searchHintsForDevice(hints, isTelevision = true).map { it.id })
        assertEquals(results, searchItemsForDevice(results, isTelevision = false))
        assertEquals(hints, searchHintsForDevice(hints, isTelevision = false))
    }

    @Test
    fun `television filter list omits books without changing touch filter list`() {
        assertEquals(false, searchFiltersForDevice(isTelevision = true).contains(SearchFilter.Books))
        assertEquals(true, searchFiltersForDevice(isTelevision = false).contains(SearchFilter.Books))
    }
}
