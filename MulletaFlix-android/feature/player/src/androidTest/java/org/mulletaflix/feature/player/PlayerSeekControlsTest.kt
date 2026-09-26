package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class PlayerSeekControlsTest {
    @get:Rule val compose = createComposeRule()

    @Test
    fun unknown_duration_disables_seek_bar_and_keeps_progress_unchanged() {
        val soughtPositions = mutableListOf<Long>()
        compose.setContent {
            MaterialTheme {
                PlayerSeekBar(
                    currentPositionMs = 0L,
                    durationMs = 0L,
                    canSeek = false,
                    onSeekPreview = soughtPositions::add,
                    onSeekFinished = soughtPositions::add,
                )
            }
        }

        compose.onNodeWithContentDescription("Posição da reprodução").assertIsNotEnabled()
        assertEquals(emptyList<Long>(), soughtPositions)
    }

    @Test
    fun known_duration_does_not_enable_seek_for_non_seekable_media() {
        val soughtPositions = mutableListOf<Long>()
        compose.setContent {
            MaterialTheme {
                PlayerSeekBar(
                    currentPositionMs = 10_000L,
                    durationMs = 60_000L,
                    canSeek = false,
                    onSeekPreview = soughtPositions::add,
                    onSeekFinished = soughtPositions::add,
                )
            }
        }

        compose.onNodeWithContentDescription("Posição da reprodução").assertIsNotEnabled()
        assertEquals(emptyList<Long>(), soughtPositions)
    }

    @Test
    fun non_seekable_media_disables_transport_seek_buttons() {
        val seekDeltas = mutableListOf<Long>()
        compose.setContent {
            MaterialTheme {
                PlayerTransportControls(
                    canSeek = false,
                    onSeekBy = seekDeltas::add,
                )
            }
        }

        compose.onNodeWithContentDescription("Avançar 10 segundos").assertIsNotEnabled()
        assertEquals(emptyList<Long>(), seekDeltas)
    }

    @Test
    fun playing_osd_auto_hides_after_three_seconds() {
        var visible by mutableStateOf(true)
        compose.mainClock.autoAdvance = false
        compose.setContent {
            MaterialTheme {
                PlayerOsdAutoHideEffect(
                    isVisible = visible,
                    isPlaying = true,
                    onHide = { visible = false },
                )
                if (visible) Text("Controles do player")
            }
        }

        compose.mainClock.advanceTimeBy(2_500L)
        compose.onNodeWithText("Controles do player").assertExists()
        compose.mainClock.advanceTimeBy(700L)
        compose.waitForIdle()
        compose.onAllNodesWithText("Controles do player").assertCountEquals(0)
    }

    @Test
    fun remote_navigation_restarts_osd_auto_hide_timeout() {
        var visible by mutableStateOf(true)
        var interactionRevision by mutableStateOf(0)
        compose.mainClock.autoAdvance = false
        compose.setContent {
            MaterialTheme {
                PlayerOsdAutoHideEffect(
                    isVisible = visible,
                    isPlaying = true,
                    interactionRevision = interactionRevision,
                    onHide = { visible = false },
                )
                if (visible) Text("Controles do player")
            }
        }

        compose.mainClock.advanceTimeBy(2_500L)
        compose.runOnIdle { interactionRevision++ }
        compose.mainClock.advanceTimeBy(2_500L)
        compose.onNodeWithText("Controles do player").assertExists()
        compose.mainClock.advanceTimeBy(600L)
        compose.waitForIdle()
        compose.onAllNodesWithText("Controles do player").assertCountEquals(0)
    }

    @Test
    fun paused_osd_remains_visible() {
        var visible by mutableStateOf(true)
        compose.mainClock.autoAdvance = false
        compose.setContent {
            MaterialTheme {
                PlayerOsdAutoHideEffect(
                    isVisible = visible,
                    isPlaying = false,
                    onHide = { visible = false },
                )
                if (visible) Text("Controles do player")
            }
        }

        compose.mainClock.advanceTimeBy(5_000L)
        compose.onNodeWithText("Controles do player").assertExists()
    }
}
