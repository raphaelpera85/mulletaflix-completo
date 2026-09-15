package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class NextEpisodePolicyTest {

    @Test
    fun `does not show next episode prompt when there is no next episode`() {
        val show = shouldShowNextEpisodePrompt(
            hasNextEpisode = false,
            isPlaybackEnded = true,
            countdownActive = true,
        )
        assertFalse(show)
    }

    @Test
    fun `shows prompt when next episode exists and countdown is active`() {
        val show = shouldShowNextEpisodePrompt(
            hasNextEpisode = true,
            isPlaybackEnded = false,
            countdownActive = true,
        )
        assertTrue(show)
    }

    @Test
    fun `shows prompt when next episode exists and playback has ended`() {
        val show = shouldShowNextEpisodePrompt(
            hasNextEpisode = true,
            isPlaybackEnded = true,
            countdownActive = false,
        )
        assertTrue(show)
    }

    @Test
    fun `formats season and episode tag correctly`() {
        assertEquals("T2 : E5", formatNextEpisodeSubtitle(seasonNumber = 2, episodeNumber = 5))
        assertEquals("Episódio 10", formatNextEpisodeSubtitle(seasonNumber = null, episodeNumber = 10))
        assertEquals(null, formatNextEpisodeSubtitle(seasonNumber = null, episodeNumber = null))
    }
}
