package org.mulletaflix.data.mapper

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.MediaStreamDto
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
    fun `maps image fallback fields correctly`() {
        val dto = BaseItemDto(
            id = "ep-1",
            name = "Ep 1",
            type = "Episode",
            primaryImageTag = "p-tag",
            seriesPrimaryImageTag = "s-tag",
            seriesThumbImageTag = "st-tag",
            parentThumbItemId = "parent-id",
            parentThumbImageTag = "pt-tag",
            parentBackdropItemId = "p-b-id",
            parentBackdropImageTags = listOf("pb-tag-1"),
            backdropImageTags = listOf("b-tag-1"),
        )

        val domain = dto.toDomain()

        assertEquals("p-tag", domain.primaryImageTag)
        assertEquals("s-tag", domain.seriesPrimaryImageTag)
        assertEquals("st-tag", domain.seriesThumbImageTag)
        assertEquals("parent-id", domain.parentThumbItemId)
        assertEquals("pt-tag", domain.parentThumbImageTag)
        assertEquals("p-b-id", domain.parentBackdropItemId)
        assertEquals(listOf("pb-tag-1"), domain.parentBackdropImageTags)
        assertEquals(listOf("b-tag-1"), domain.backdropImageTags)
    }
}
