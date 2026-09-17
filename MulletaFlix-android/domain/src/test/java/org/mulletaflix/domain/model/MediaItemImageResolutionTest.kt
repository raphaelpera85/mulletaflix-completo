package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Test

class MediaItemImageResolutionTest {

    @Test
    fun `resolves direct primary image tag when available`() {
        val item = MediaItem(
            id = "item-1",
            name = "Movie 1",
            type = MediaItemType.Movie,
            imageTags = mapOf(ImageType.Primary to "primary-tag-123"),
            backdropImageTags = listOf("backdrop-tag-456"),
        )

        assertEquals("Items/item-1/Images/Primary?tag=primary-tag-123", item.primaryImageUrl)
        assertEquals("Items/item-1/Images/Backdrop?tag=backdrop-tag-456", item.backdropImageUrl)
    }

    @Test
    fun `falls back to backdrop when primary image tag is missing - older media case`() {
        // Many older items on Jellyfin only have BackdropImageTags and empty ImageTags
        val item = MediaItem(
            id = "old-movie-1",
            name = "A Mulher na Cabine 10",
            type = MediaItemType.Movie,
            imageTags = emptyMap(),
            backdropImageTags = listOf("backdrop-tag-789"),
        )

        assertEquals("Items/old-movie-1/Images/Backdrop?tag=backdrop-tag-789", item.primaryImageUrl)
        assertEquals("Items/old-movie-1/Images/Backdrop?tag=backdrop-tag-789", item.backdropImageUrl)
    }

    @Test
    fun `falls back to series primary image for episodes lacking individual primary image`() {
        val episode = MediaItem(
            id = "ep-101",
            name = "Pilot",
            type = MediaItemType.Episode,
            seriesId = "series-999",
            seriesPrimaryImageTag = "series-primary-tag",
            imageTags = emptyMap(),
        )

        assertEquals("Items/series-999/Images/Primary?tag=series-primary-tag", episode.primaryImageUrl)
    }

    @Test
    fun `falls back to thumb when primary and backdrop are missing`() {
        val item = MediaItem(
            id = "thumb-item",
            name = "Audio Track",
            type = MediaItemType.Audio,
            imageTags = mapOf(ImageType.Thumb to "thumb-tag-555"),
        )

        assertEquals("Items/thumb-item/Images/Thumb?tag=thumb-tag-555", item.primaryImageUrl)
    }

    @Test
    fun `falls back to parent thumb when episode has parent thumb`() {
        val episode = MediaItem(
            id = "ep-202",
            name = "Episode 2",
            type = MediaItemType.Episode,
            parentThumbItemId = "season-2",
            parentThumbImageTag = "season-thumb-tag",
        )

        assertEquals("Items/season-2/Images/Thumb?tag=season-thumb-tag", episode.primaryImageUrl)
    }

    @Test
    fun `falls back to direct Primary endpoint when no tags are provided`() {
        val item = MediaItem(
            id = "raw-item-1",
            name = "Direct Item",
            type = MediaItemType.Movie,
        )

        assertEquals("Items/raw-item-1/Images/Primary", item.primaryImageUrl)
    }
}
