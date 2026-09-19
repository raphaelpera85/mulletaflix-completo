package org.mulletaflix.feature.player

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackProgressPolicyTest {

    @Test
    fun `playing sessions are reported as active`() {
        assertFalse(isPlaybackPausedForReport(isPlaying = true))
    }

    @Test
    fun `paused sessions are reported as paused`() {
        assertTrue(isPlaybackPausedForReport(isPlaying = false))
    }
}
