package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MediaItemPresentationTest {
    @Test
    fun moviesAndSeriesUseCompletePosterArtwork() {
        assertTrue(MediaItemType.Movie.usesPosterArtwork())
        assertTrue(MediaItemType.Series.usesPosterArtwork())
        assertTrue(MediaItemType.MusicAlbum.usesPosterArtwork())
        assertTrue(MediaItemType.Book.usesPosterArtwork())
    }

    @Test
    fun episodicAndLiveContentDoNotUsePosterArtwork() {
        assertFalse(MediaItemType.Season.usesPosterArtwork())
        assertFalse(MediaItemType.Episode.usesPosterArtwork())
        assertFalse(MediaItemType.LiveTvChannel.usesPosterArtwork())
        assertFalse(MediaItemType.LiveTvProgram.usesPosterArtwork())
    }

    @Test
    fun episodeCardMetadataIsSharedAcrossFeatures() {
        assertEquals(
            "T02 · E07",
            MediaItem("episode", "Episódio", MediaItemType.Episode, parentIndexNumber = 2, indexNumber = 7).cardMetadata(),
        )
        assertEquals(
            "Temporada 3",
            MediaItem("season", "Temporada", MediaItemType.Season, parentIndexNumber = 3).cardMetadata(),
        )
    }

    @Test
    fun cardMetadataIsAbsentWhenEpisodeNumberingIsIncomplete() {
        assertEquals(null, MediaItem("episode", "Episódio", MediaItemType.Episode, indexNumber = 7).cardMetadata())
    }
}
