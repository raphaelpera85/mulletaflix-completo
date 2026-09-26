package org.mulletaflix.feature.home

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.UserProgress
import org.mulletaflix.domain.model.cardMetadata

class HomePresentationTest {
    @Test
    fun `resume movie and series use vertical posters`() {
        assertEquals(
            MediaCardShape.Portrait,
            defaultMediaSectionShape(MediaItem("movie", "Filme", MediaItemType.Movie)),
        )
        assertEquals(
            MediaCardShape.Portrait,
            defaultMediaSectionShape(MediaItem("series", "Série", MediaItemType.Series)),
        )
    }

    @Test
    fun `episodes and live channels keep landscape artwork`() {
        assertEquals(
            MediaCardShape.Landscape,
            defaultMediaSectionShape(MediaItem("episode", "Episódio", MediaItemType.Episode)),
        )
        assertEquals(
            MediaCardShape.Landscape,
            defaultMediaSectionShape(MediaItem("channel", "Canal", MediaItemType.LiveTvChannel)),
        )
    }

    @Test
    fun `episode cards expose season and episode metadata`() {
        assertEquals(
            "T02 · E07",
            MediaItem(
                id = "episode",
                name = "Episódio",
                type = MediaItemType.Episode,
                parentIndexNumber = 2,
                indexNumber = 7,
            ).cardMetadata(),
        )
    }

    @Test
    fun `episode metadata is omitted when numbering is incomplete`() {
        assertEquals(
            null,
            MediaItem("episode", "Episódio", MediaItemType.Episode, indexNumber = 7).cardMetadata(),
        )
    }

    @Test
    fun `resume action requires a positive saved position and an unfinished item`() {
        assertTrue(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                playbackPositionTicks = 10,
            ).hasResumablePlaybackPosition(),
        )
        assertTrue(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                userProgress = UserProgress(playbackPositionTicks = 10),
            ).hasResumablePlaybackPosition(),
        )
        assertTrue(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                playbackPositionTicks = 9,
                runtimeTicks = 10,
            ).hasResumablePlaybackPosition(),
        )
        assertFalse(MediaItem("movie", "Filme", MediaItemType.Movie).hasResumablePlaybackPosition())
        assertFalse(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                playbackPositionTicks = 0,
            ).hasResumablePlaybackPosition(),
        )
        assertFalse(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                playbackPositionTicks = -1,
                userProgress = UserProgress(playbackPositionTicks = 10),
            ).hasResumablePlaybackPosition(),
        )
        assertFalse(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                playbackPositionTicks = 10,
                runtimeTicks = 10,
            ).hasResumablePlaybackPosition(),
        )
        assertFalse(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                playbackPositionTicks = 11,
                runtimeTicks = 10,
            ).hasResumablePlaybackPosition(),
        )
        assertFalse(
            MediaItem(
                "movie",
                "Filme",
                MediaItemType.Movie,
                playbackPositionTicks = 10,
                isPlayed = true,
            ).hasResumablePlaybackPosition(),
        )
    }
}
