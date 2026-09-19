package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class PlaybackSpeedPolicyTest {

    @Test
    fun `keeps supported speed unchanged`() {
        assertEquals(1.5f, normalizePlaybackSpeed(1.5f))
    }

    @Test
    fun `clamps speed to player limits`() {
        assertEquals(0.5f, normalizePlaybackSpeed(0.1f))
        assertEquals(2f, normalizePlaybackSpeed(3f))
    }

    @Test
    fun `uses normal speed for non finite values`() {
        assertEquals(1f, normalizePlaybackSpeed(Float.NaN))
        assertEquals(1f, normalizePlaybackSpeed(Float.POSITIVE_INFINITY))
        assertEquals(1f, normalizePlaybackSpeed(Float.NEGATIVE_INFINITY))
    }
}
