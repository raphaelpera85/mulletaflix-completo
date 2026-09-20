package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackReportPolicyTest {

    @Test
    fun `progress snapshot keeps track selection from the same event`() {
        val snapshot = PlaybackProgressSnapshot(
            generation = 7L,
            itemId = "movie",
            playSessionId = "session-1",
            mediaSourceId = "source-1",
            audioIndex = 4,
            subtitleIndex = 9,
            positionMs = 12_345L,
            isPaused = false,
        )

        assertEquals(4, snapshot.audioIndex)
        assertEquals(9, snapshot.subtitleIndex)
        assertEquals(12_345L, snapshot.positionMs)
        assertFalse(snapshot.isPaused)
    }

    @Test
    fun `invalidates a report when the load generation changes`() {
        assertFalse(
            isCurrentPlaybackReport(
                expectedGeneration = 7L,
                currentGeneration = 8L,
                expectedItemId = "movie",
                currentItemId = "movie",
                expectedPlaySessionId = "session-1",
                currentPlaySessionId = "session-1",
                expectedMediaSourceId = "source-1",
                currentMediaSourceId = "source-1",
            ),
        )
    }

    @Test
    fun `invalidates a report when the same item starts a new server session`() {
        assertFalse(
            isCurrentPlaybackReport(
                expectedGeneration = 7L,
                currentGeneration = 7L,
                expectedItemId = "movie",
                currentItemId = "movie",
                expectedPlaySessionId = "session-1",
                currentPlaySessionId = "session-2",
                expectedMediaSourceId = "source-1",
                currentMediaSourceId = "source-1",
            ),
        )
    }

    @Test
    fun `accepts a report for the same playback session and source`() {
        assertTrue(
            isCurrentPlaybackReport(
                expectedGeneration = 7L,
                currentGeneration = 7L,
                expectedItemId = "movie",
                currentItemId = "movie",
                expectedPlaySessionId = "session-1",
                currentPlaySessionId = "session-1",
                expectedMediaSourceId = "source-1",
                currentMediaSourceId = "source-1",
            ),
        )
    }
}
