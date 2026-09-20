package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class PlaybackRetryPositionTest {
    @Test
    fun `retry keeps the furthest known position`() {
        assertEquals(42_000L, playbackRetryPosition(0L, 42_000L))
        assertEquals(48_000L, playbackRetryPosition(48_000L, 42_000L))
    }

    @Test
    fun `retry never returns a negative position`() {
        assertEquals(0L, playbackRetryPosition(-1L, -5L))
    }

    @Test
    fun `network recovery reuses the error position when the player resets`() {
        assertEquals(95_000L, playbackRetryPosition(0L, 95_000L))
    }
}
