package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
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
}
