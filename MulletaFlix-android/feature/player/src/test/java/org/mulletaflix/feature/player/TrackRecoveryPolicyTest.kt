package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class TrackRecoveryPolicyTest {
    @Test
    fun `keeps manually selected audio and subtitle streams`() {
        assertEquals(
            TrackRecoverySelection(audioStreamIndex = 4, subtitleStreamIndex = 7, subtitlesDisabled = false),
            trackRecoverySelection(audioStreamIndex = 4, subtitleStreamIndex = 7, subtitlesDisabled = false),
        )
    }

    @Test
    fun `keeps subtitles disabled when no subtitle stream is selected`() {
        val selection = trackRecoverySelection(audioStreamIndex = 4, subtitleStreamIndex = null, subtitlesDisabled = false)
        assertTrue(selection.subtitlesDisabled)
        assertEquals(4, selection.audioStreamIndex)
    }
}
