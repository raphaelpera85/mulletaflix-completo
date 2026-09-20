package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Test

class PlaybackProgressTest {
    private fun item(percentage: Double?) = MediaItem(
        id = "item",
        name = "Item",
        type = MediaItemType.Movie,
        playedPercentage = percentage,
    )

    @Test
    fun `percentage is converted and bounded to compose fraction`() {
        assertEquals(0f, item(null).playbackProgressFraction())
        assertEquals(0f, item(-10.0).playbackProgressFraction())
        assertEquals(0.42f, item(42.0).playbackProgressFraction())
        assertEquals(1f, item(125.0).playbackProgressFraction())
    }

    @Test
    fun `non finite percentage is treated as missing`() {
        assertEquals(0f, item(Double.NaN).playbackProgressFraction())
        assertEquals(0f, item(Double.POSITIVE_INFINITY).playbackProgressFraction())
        assertEquals(0f, item(Double.NEGATIVE_INFINITY).playbackProgressFraction())
    }
}
