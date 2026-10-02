package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackSessionDiagnosticsTest {

    @Test
    fun `startup is measured from prepare to first rendered video frame once`() {
        val diagnostics = PlaybackSessionDiagnostics()
        diagnostics.reset()
        diagnostics.onPrepared(nowMs = 100L)
        diagnostics.onFirstVideoFrameRendered(renderTimeMs = 1_425L)
        diagnostics.onFirstVideoFrameRendered(renderTimeMs = 2_000L)

        assertEquals(1_325L, diagnostics.snapshot(nowMs = 2_000L).firstVideoFrameMs)
    }

    @Test
    fun `buffer durations exclude user pause and initial startup is not a rebuffer`() {
        val diagnostics = PlaybackSessionDiagnostics()
        diagnostics.reset()
        diagnostics.onPrepared(nowMs = 100L)
        diagnostics.onPlaybackStateChanged(isBuffering = true, nowMs = 120L)
        diagnostics.onPlayWhenReadyChanged(value = true, nowMs = 130L)
        diagnostics.onFirstVideoFrameRendered(renderTimeMs = 250L)
        diagnostics.onPlaybackStateChanged(isBuffering = false, nowMs = 280L)

        diagnostics.onPlaybackStateChanged(isBuffering = true, nowMs = 400L)
        assertEquals(1, diagnostics.snapshot(nowMs = 500L).bufferingEpisodes)
        diagnostics.onPlayWhenReadyChanged(value = false, nowMs = 600L)
        assertEquals(350L, diagnostics.snapshot(nowMs = 5_000L).bufferingDurationMs)

        diagnostics.onPlayWhenReadyChanged(value = true, nowMs = 5_000L)
        val resumedBuffering = diagnostics.snapshot(nowMs = 5_100L)
        assertEquals(2, resumedBuffering.bufferingEpisodes)
        assertEquals(450L, resumedBuffering.bufferingDurationMs)
        assertTrue(resumedBuffering.isBuffering)
    }

    @Test
    fun `pause before buffer clears interval without counting it`() {
        val diagnostics = PlaybackSessionDiagnostics()
        diagnostics.onPlaybackStateChanged(isBuffering = true, nowMs = 10L)
        diagnostics.onPlayWhenReadyChanged(value = true, nowMs = 20L)
        diagnostics.onPlayWhenReadyChanged(value = false, nowMs = 70L)

        val snapshot = diagnostics.snapshot(nowMs = 1_000L)
        assertEquals(0, snapshot.bufferingEpisodes)
        assertEquals(50L, snapshot.bufferingDurationMs)
        assertFalse(snapshot.isBuffering)
    }

    @Test
    fun `video format rejects missing fields and dropped frame count ignores invalid values`() {
        val diagnostics = PlaybackSessionDiagnostics()
        diagnostics.onVideoFormatChanged(1920, 1080, "avc1.640028", 8_250_000L)
        diagnostics.onDroppedVideoFrames(4)
        diagnostics.onDroppedVideoFrames(0)
        diagnostics.onDroppedVideoFrames(-2)
        assertEquals("1920x1080 · avc1.640028 · 8250 kbps", diagnostics.snapshot(0L).activeVideoFormat)
        assertEquals(4, diagnostics.snapshot(0L).droppedVideoFrames)

        diagnostics.onVideoFormatChanged(-1, 0, null, -1L)
        assertNull(diagnostics.snapshot(0L).activeVideoFormat)
    }

    @Test
    fun `reset isolates playback measurements between media items`() {
        val diagnostics = PlaybackSessionDiagnostics()
        diagnostics.onPrepared(10L)
        diagnostics.onFirstVideoFrameRendered(20L)
        diagnostics.onDroppedVideoFrames(8)
        diagnostics.onVideoFormatChanged(1280, 720, "avc1", 4_000_000L)
        diagnostics.reset()

        assertEquals(PlaybackSessionMetrics(), diagnostics.snapshot(100L))
    }

    @Test
    fun `playback duration uses stable millisecond and second labels`() {
        assertEquals("999 ms", formatPlaybackDuration(999L))
        assertEquals("1.2 s", formatPlaybackDuration(1_250L))
    }
}
