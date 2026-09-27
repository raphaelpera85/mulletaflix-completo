package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata

class PlaybackMetadataTest {

    @Test
    fun `episode title includes series season and episode`() {
        assertEquals(
            "The Expanse · S02E03 · Static",
            mediaNotificationTitle(
                MediaItem(
                    id = "episode-1",
                    name = "Static",
                    type = MediaItemType.Episode,
                    seriesName = "The Expanse",
                    parentIndexNumber = 2,
                    indexNumber = 3,
                ),
            ),
        )
    }

    @Test
    fun `movie title remains unchanged`() {
        assertEquals(
            "A New Movie",
            mediaNotificationTitle(
                MediaItem(id = "movie-1", name = "A New Movie", type = MediaItemType.Movie),
            ),
        )
    }

    @Test
    fun `offline episode title includes persisted series and episode context`() {
        assertEquals(
            "The Expanse · S02E03 · Static",
            offlineMediaNotificationTitle(
                title = "Static",
                episode = DownloadEpisodeMetadata("series-1", 2, 3, "The Expanse"),
            ),
        )
    }

    @Test
    fun `offline media without episode metadata keeps its original title`() {
        assertEquals("A New Movie", offlineMediaNotificationTitle("A New Movie", null))
    }
}
