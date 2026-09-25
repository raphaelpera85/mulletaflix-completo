package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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

    @Test
    fun `keeps recovery pending until an advertised track becomes selectable`() {
        val firstTracksEvent = shouldRetryTrackSelection(7, listOf(4, 7), selected = false)
        assertTrue(firstTracksEvent)

        val nextTracksEvent = shouldRetryTrackSelection(7, listOf(4, 7), selected = true)
        assertFalse(nextTracksEvent)
    }

    @Test
    fun `does not retry a selected or no longer advertised track`() {
        assertFalse(shouldRetryTrackSelection(7, listOf(7), selected = true))
        assertFalse(shouldRetryTrackSelection(7, listOf(4), selected = false))
    }
}
