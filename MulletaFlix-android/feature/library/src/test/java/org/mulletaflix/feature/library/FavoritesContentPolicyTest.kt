package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class FavoritesContentPolicyTest {
    @Test
    fun `tv hides books from favorites but handhelds keep them`() {
        val movie = MediaItem("movie", "Filme", MediaItemType.Movie)
        val book = MediaItem("book", "Livro", MediaItemType.Book)
        val audiobook = MediaItem("audiobook", "Audiolivro", MediaItemType.AudioBook)
        val items = listOf(movie, book, audiobook)

        assertEquals(listOf(movie), favoritesItemsForDevice(items, isTelevision = true))
        assertEquals(items, favoritesItemsForDevice(items, isTelevision = false))
    }
}
