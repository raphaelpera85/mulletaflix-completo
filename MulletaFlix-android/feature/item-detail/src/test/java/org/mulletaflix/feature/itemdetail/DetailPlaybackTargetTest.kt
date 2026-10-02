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
    fun `series with episodes can play`() {
        val series = MediaItem("s1", "Series", MediaItemType.Series)
        assertTrue(canPlayItem(series, listOf(firstEpisode)))
    }

    @Test
    fun `book opens reader on handheld devices`() {
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)

        assertEquals(
            DetailPlaybackTarget.ReadBook("book-1"),
            detailPlaybackTarget(book, emptyList(), isTelevision = false),
        )
    }

    @Test
    fun `book reader action is unavailable on television`() {
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)

        assertEquals(
            DetailPlaybackTarget.Unavailable,
            detailPlaybackTarget(book, emptyList(), isTelevision = true),
        )
    }

    @Test
    fun `series without episodes keeps disabled video action`() {
        val series = MediaItem("s1", "Series", MediaItemType.Series)

        assertEquals(
            DetailPlaybackTarget.PlayVideo("s1", enabled = false),
            detailPlaybackTarget(series, emptyList(), isTelevision = false),
        )
    }
}
