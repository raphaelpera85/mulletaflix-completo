package org.mulletaflix.data.mapper

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.MediaStreamDto
import org.mulletaflix.data.db.MediaItemEntity
import org.mulletaflix.domain.model.ImageType
import org.mulletaflix.domain.model.MediaItemType

class MediaMapperTest {

    @Test
    fun `maps server media metadata and capabilities`() {
        val item = BaseItemDto(
            id = "movie-1",
            name = "Mullet Movie",
            type = "Movie",
            imageTags = mapOf("Primary" to "tag-primary"),
            mediaStreams = listOf(
                MediaStreamDto(type = "Video", width = 3840, height = 2160, displayTitle = "4K HDR Dolby Vision"),
                MediaStreamDto(type = "Audio", displayTitle = "Português Atmos"),
            ),
        ).toDomain()

        assertEquals(MediaItemType.Movie, item.type)
        assertEquals("tag-primary", item.imageTags[ImageType.Primary])
        assertTrue(item.has4K)
        assertTrue(item.hasHdr)
        assertTrue(item.hasDolbyVision)
        assertTrue(item.hasAtmos)
    }

    @Test
    fun `entity round trip preserves offline playback fields`() {
        val entity = MediaItemEntity(
            id = "episode-1", name = "Episode", type = "Episode", overview = "Overview",
            year = 2026, runtimeTicks = 100L, isFavorite = true, isPlayed = false,
            playedPercentage = 42.0, playbackPositionTicks = 50L, primaryImageTag = "p",
            backdropImageTag = "b", seriesId = "series-1", seriesName = "Series",
            seasonId = "season-1", indexNumber = 2, parentIndexNumber = 1, userId = "user-1",
        )

        val restored = entity.toDomain().toEntity("user-1")

        assertEquals(entity.id, restored.id)
        assertEquals(entity.type, restored.type)
        assertEquals(entity.playbackPositionTicks, restored.playbackPositionTicks)
        assertEquals(entity.primaryImageTag, restored.primaryImageTag)
        assertEquals(entity.userId, restored.userId)
    }
}
