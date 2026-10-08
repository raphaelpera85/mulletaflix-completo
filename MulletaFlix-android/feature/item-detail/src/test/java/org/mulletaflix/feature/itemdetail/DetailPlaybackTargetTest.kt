package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class DetailPlaybackTargetTest {

    private val firstEpisode = MediaItem("ep-1", "Piloto", MediaItemType.Episode)

    @Test
    fun `series plays its first loaded episode`() {
        val series = MediaItem("s1", "Series", MediaItemType.Series)
        assertEquals("ep-1", playbackTargetId(series, listOf(firstEpisode)))
    }

    @Test
    fun `season plays its first loaded episode`() {
        val season = MediaItem("sea-1", "Temporada 1", MediaItemType.Season)
        assertEquals("ep-1", playbackTargetId(season, listOf(firstEpisode)))
    }

    @Test
    fun `container without episodes falls back to its own id and cannot play`() {
        val series = MediaItem("s1", "Series", MediaItemType.Series)
        assertEquals("s1", playbackTargetId(series, emptyList()))
        assertFalse(canPlayItem(series, emptyList()))
    }

    @Test
    fun `playable items play themselves`() {
        listOf(
            MediaItem("m1", "Movie", MediaItemType.Movie),
            firstEpisode,
            MediaItem("al-1", "Album", MediaItemType.MusicAlbum),
            MediaItem("ch-1", "Channel", MediaItemType.LiveTvChannel),
        ).forEach { item ->
            assertEquals(item.id, playbackTargetId(item, listOf(firstEpisode)))
            assertTrue(canPlayItem(item, emptyList()))
        }
    }

    @Test
    fun `books open the reader and never the video player`() {
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)

        assertEquals(DetailPrimaryAction.ReadBook, detailPrimaryAction(book))
        assertFalse(canPlayItem(book, emptyList()))
        assertTrue(canReadBookOnDevice(book, isTelevision = false))
        assertFalse(canReadBookOnDevice(book, isTelevision = true))
    }

    @Test
    fun `audiobooks remain playable media and are not sent to the text reader`() {
        val audiobook = MediaItem("audio-book-1", "Audiolivro", MediaItemType.AudioBook)

        assertEquals(DetailPrimaryAction.PlayVideo, detailPrimaryAction(audiobook))
        assertTrue(canPlayItem(audiobook, emptyList()))
        assertFalse(canReadBookOnDevice(audiobook, isTelevision = false))
    }

    @Test
    fun `book details are unavailable on television but remain available on handhelds`() {
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)
        val audiobook = MediaItem("audio-book-1", "Audiolivro", MediaItemType.AudioBook)
        val movie = MediaItem("movie-1", "Filme", MediaItemType.Movie)

        assertFalse(canShowItemDetailsOnDevice(book, isTelevision = true))
        assertFalse(canShowItemDetailsOnDevice(audiobook, isTelevision = true))
        assertTrue(canShowItemDetailsOnDevice(book, isTelevision = false))
        assertTrue(canShowItemDetailsOnDevice(audiobook, isTelevision = false))
        assertTrue(canShowItemDetailsOnDevice(movie, isTelevision = true))
    }

    @Test
    fun `television hides book recommendations but handhelds keep them`() {
        val movie = MediaItem("movie-1", "Filme recomendado", MediaItemType.Movie)
        val book = MediaItem("book-1", "Livro recomendado", MediaItemType.Book)
        val audiobook = MediaItem("audio-book-1", "Audiolivro recomendado", MediaItemType.AudioBook)
        val recommendations = listOf(movie, book, audiobook)

        assertEquals(listOf(movie), similarItemsForDevice(recommendations, isTelevision = true))
        assertEquals(recommendations, similarItemsForDevice(recommendations, isTelevision = false))
    }

    @Test
    fun `non-book detail actions stay on the video and media player path`() {
        assertEquals(
            DetailPrimaryAction.PlayVideo,
            detailPrimaryAction(MediaItem("movie-1", "Filme", MediaItemType.Movie)),
        )
    }

    @Test
    fun `series with episodes can play`() {
        val series = MediaItem("s1", "Series", MediaItemType.Series)
        assertTrue(canPlayItem(series, listOf(firstEpisode)))
    }
}
