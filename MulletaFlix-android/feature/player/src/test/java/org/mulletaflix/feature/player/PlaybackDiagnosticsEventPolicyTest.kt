package org.mulletaflix.feature.player

import androidx.media3.exoplayer.source.MediaSource
import androidx.media3.common.Format
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackDiagnosticsEventPolicyTest {

    @Test
    fun `accepts events for the currently playing period`() {
        val eventPeriod = MediaSource.MediaPeriodId("current-item", 1L)
        val currentPeriod = MediaSource.MediaPeriodId("current-item", 1L)

        assertTrue(isPlaybackDiagnosticsEventForCurrentPeriod(eventPeriod, currentPeriod))
    }

    @Test
    fun `accepts current period with updated next-ad boundary`() {
        val eventPeriod = MediaSource.MediaPeriodId("item", 1L, 2)
        val currentPeriod = MediaSource.MediaPeriodId("item", 1L, 3)

        assertTrue(isPlaybackDiagnosticsEventForCurrentPeriod(eventPeriod, currentPeriod))
    }

    @Test
    fun `ignores analytics from a preloaded next item`() {
        val currentPeriod = MediaSource.MediaPeriodId("current-item", 1L)
        val queuedNextPeriod = MediaSource.MediaPeriodId("next-item", 2L)

        assertFalse(isPlaybackDiagnosticsEventForCurrentPeriod(queuedNextPeriod, currentPeriod))
    }

    @Test
    fun `ignores same timeline period from another playback window sequence`() {
        val activePeriod = MediaSource.MediaPeriodId("item", 1L)
        val staleWindowPeriod = MediaSource.MediaPeriodId("item", 2L)

        assertFalse(isPlaybackDiagnosticsEventForCurrentPeriod(staleWindowPeriod, activePeriod))
    }

    @Test
    fun `ignores events without a current media period`() {
        val eventPeriod = MediaSource.MediaPeriodId("item", 1L)

        assertFalse(isPlaybackDiagnosticsEventForCurrentPeriod(eventPeriod, null))
        assertFalse(isPlaybackDiagnosticsEventForCurrentPeriod(null, eventPeriod))
    }

    @Test
    fun `promotes preloaded video format only when transition period is active`() {
        val pending = PendingPlaybackVideoFormats()
        val queuedPeriod = MediaSource.MediaPeriodId("next-item", 2L)
        val format = Format.Builder().setWidth(1920).setHeight(1080).build()

        pending.enqueue(queuedPeriod, format)

        assertEquals(
            null,
            takePlaybackVideoFormatForActiveTransition(
                pending,
                transitionPeriodId = queuedPeriod,
                currentPeriodId = MediaSource.MediaPeriodId("current-item", 1L),
            ),
        )
        assertEquals(
            format,
            takePlaybackVideoFormatForActiveTransition(pending, queuedPeriod, queuedPeriod),
        )
        assertEquals(null, takePlaybackVideoFormatForActiveTransition(pending, queuedPeriod, queuedPeriod))
    }

    @Test
    fun `clears queued format after Cast invalidates local diagnostics`() {
        val pending = PendingPlaybackVideoFormats()
        val period = MediaSource.MediaPeriodId("item", 1L)
        pending.enqueue(period, Format.Builder().setWidth(1280).setHeight(720).build())

        pending.clear()

        assertEquals(null, pending.takeFor(period))
    }
}
