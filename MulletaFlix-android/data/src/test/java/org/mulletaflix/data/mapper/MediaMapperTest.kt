package org.mulletaflix.data.mapper

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.MediaSourceDto
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

    @Test
    fun `preserves server-selected audio and subtitle stream indices`() {
        val domain = MediaSourceDto(
            id = "source-1",
            defaultAudioStreamIndex = 7,
            defaultSubtitleStreamIndex = -1,
        ).toDomain()

        assertEquals(7, domain.defaultAudioStreamIndex)
        // -1 is the server's explicit "subtitles disabled" choice; it must not
        // become null and accidentally fall back to a container default.
        assertEquals(-1, domain.defaultSubtitleStreamIndex)
    }

    @Test
    fun `maps external subtitle delivery metadata`() {
        val stream = MediaStreamDto(
            type = "Subtitle",
            index = 12,
            codec = "SubRip",
            isExternal = true,
            deliveryUrl = "/Items/movie-1/Subtitles/12/0/Stream.srt",
        ).toDomain()

        assertEquals(12, stream.index)
        assertTrue(stream.isExternal)
        assertEquals("/Items/movie-1/Subtitles/12/0/Stream.srt", stream.deliveryUrl)
    }
}
