package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class PlaylistLibraryContentPolicyTest {
    @Test
    fun `television hides books while handhelds retain all playlist items`() {
        val movie = MediaItem("movie", "Filme", MediaItemType.Movie)
        val book = MediaItem("book", "Livro", MediaItemType.Book)
        val audiobook = MediaItem("audiobook", "Audiolivro", MediaItemType.AudioBook)
        val items = listOf(movie, book, audiobook)

        assertEquals(listOf(movie), playlistItemsForDevice(items, isTelevision = true))
        assertEquals(items, playlistItemsForDevice(items, isTelevision = false))
    }

    @Test
    fun `visible playlist count uses correct Portuguese singular`() {
        assertEquals("1 título visível", playlistVisibleCountLabel(1))
        assertEquals("2 títulos visíveis", playlistVisibleCountLabel(2))
    }
}
