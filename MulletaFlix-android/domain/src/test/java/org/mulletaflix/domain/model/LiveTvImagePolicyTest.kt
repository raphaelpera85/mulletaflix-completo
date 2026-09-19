package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Test

class LiveTvImagePolicyTest {
    @Test
    fun `live tv channel resolves its authenticated primary image path`() {
        val channel = MediaItem(
            id = "channel-1",
            name = "Canal Mullet",
            type = MediaItemType.LiveTvChannel,
            imageTags = mapOf(ImageType.Primary to "logo-tag"),
        )

        assertEquals("Items/channel-1/Images/Primary?tag=logo-tag", channel.primaryImageUrl)
    }

    @Test
    fun `live tv channel falls back to server primary endpoint without a tag`() {
        val channel = MediaItem(
            id = "channel-2",
            name = "Canal sem logo",
            type = MediaItemType.LiveTvChannel,
        )

        assertEquals("Items/channel-2/Images/Primary", channel.primaryImageUrl)
    }
}
