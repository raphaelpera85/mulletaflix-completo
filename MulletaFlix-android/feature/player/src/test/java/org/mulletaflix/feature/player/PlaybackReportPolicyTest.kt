package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackReportPolicyTest {

    @Test
    fun `remote session is reported before switching to another item`() {
        assertTrue(
            shouldReportRemotePlaybackBeforeLoad(
                currentItemId = "movie-a",
                isOfflinePlayback = false,
                playSessionId = "session-a",
                mediaSourceId = "source-a",
                stoppedReported = false,
            ),
        )
    }

    @Test
    fun `offline playback and unprepared items do not report a remote stop`() {
        assertFalse(
            shouldReportRemotePlaybackBeforeLoad(
                currentItemId = "download-a",
                isOfflinePlayback = true,
                playSessionId = null,
                mediaSourceId = null,
                stoppedReported = false,
            ),
        )
        assertFalse(
            shouldReportRemotePlaybackBeforeLoad(
                currentItemId = "movie-a",
                isOfflinePlayback = false,
                playSessionId = null,
                mediaSourceId = null,
                stoppedReported = false,
            ),
        )
    }

    @Test
    fun `already reported session is not reported twice`() {
        assertFalse(
            shouldReportRemotePlaybackBeforeLoad(
                currentItemId = "movie-a",
                isOfflinePlayback = false,
                playSessionId = "session-a",
                mediaSourceId = "source-a",
                stoppedReported = true,
            ),
        )
    }

    @Test
    fun `progress snapshot keeps track selection from the same event`() {
        val snapshot = PlaybackProgressSnapshot(
            generation = 7L,
            sessionGeneration = 3L,
            userId = "user-1",
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
        assertEquals(3L, snapshot.sessionGeneration)
        assertEquals("user-1", snapshot.userId)
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

    @Test
    fun `invalidates progress after account or playback session changes`() {
        assertFalse(
            isCurrentPlaybackReport(
                expectedGeneration = 7L,
                currentGeneration = 7L,
                expectedItemId = "movie",
                currentItemId = "movie",
                expectedPlaySessionId = "session-1",
                currentPlaySessionId = "session-1",
                expectedMediaSourceId = "source-1",
                currentMediaSourceId = "source-1",
                expectedSessionGeneration = 2L,
                currentSessionGeneration = 3L,
                expectedUserId = "user-1",
                currentUserId = "user-2",
            ),
        )
    }
}
