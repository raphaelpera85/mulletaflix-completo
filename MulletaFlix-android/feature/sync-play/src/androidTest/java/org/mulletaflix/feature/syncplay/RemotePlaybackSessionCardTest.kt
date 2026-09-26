package org.mulletaflix.feature.syncplay

import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.repository.RemotePlaybackSession
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackRepository

class RemotePlaybackSessionCardTest {
    @get:Rule val compose = createComposeRule()

    @Test
    fun active_session_exposes_accessible_pause_seek_and_stop_controls() {
        val seeks = mutableListOf<Long>()
        var toggles = 0
        var stops = 0
        compose.setContent {
            RemotePlaybackSessionCard(
                session = RemotePlaybackSession("tv", "Sala", "Android TV", "Filme teste", false, true, 100_000_000L),
                busy = false,
                onToggle = { toggles++ },
                onSeek = seeks::add,
                onStop = { stops++ },
            )
        }

        compose.onNodeWithText("Filme teste").assertIsDisplayed()
        compose.onNodeWithText("Pausar").performClick()
        compose.onNodeWithContentDescription("Voltar 30 segundos").performClick()
        compose.onNodeWithContentDescription("Avançar 30 segundos").performClick()
        compose.onNodeWithContentDescription("Parar reprodução").performClick()

        assertEquals(1, toggles)
        assertEquals(listOf(0L, 400_000_000L), seeks)
        assertEquals(1, stops)
    }

    @Test
    fun forward_seek_stops_at_known_media_duration() {
        val seeks = mutableListOf<Long>()
        compose.setContent {
            RemotePlaybackSessionCard(
                session = RemotePlaybackSession(
                    "tv",
                    "Sala",
                    "Android TV",
                    "Filme teste",
                    false,
                    true,
                    100_000_000L,
                    durationTicks = 350_000_000L,
                ),
                busy = false,
                onToggle = {},
                onSeek = seeks::add,
                onStop = {},
            )
        }

        compose.onNodeWithContentDescription("Avançar 30 segundos").performClick()

        assertEquals(listOf(350_000_000L), seeks)
    }

    @Test
    fun known_duration_shows_clamped_progress_and_elapsed_and_total_time() {
        compose.setContent {
            RemotePlaybackSessionCard(
                session = RemotePlaybackSession(
                    "tv", "Sala", "Android TV", "Filme teste", false, true,
                    positionTicks = 600_000_000L,
                    durationTicks = 1_200_000_000L,
                ),
                busy = false,
                onToggle = {},
                onSeek = {},
                onStop = {},
            )
        }

        compose.onNodeWithText("1:00").assertIsDisplayed()
        compose.onNodeWithText("2:00").assertIsDisplayed()
        compose.onNodeWithContentDescription("Progresso da reprodução: 1:00 de 2:00").assertIsDisplayed()
    }

    @Test
    fun unknown_or_zero_duration_hides_progress_for_live_sessions() {
        compose.setContent {
            androidx.compose.foundation.layout.Column {
                listOf(null, 0L).forEachIndexed { index, duration ->
                RemotePlaybackSessionCard(
                    session = RemotePlaybackSession(
                        "tv-$index", "Sala", "Android TV", "Canal ao vivo $index", false, false,
                        positionTicks = 10_000_000L,
                        durationTicks = duration,
                    ),
                    busy = false,
                    onToggle = {},
                    onSeek = {},
                    onStop = {},
                )
            }
            }
        }
        compose.onAllNodes(hasContentDescription("Progresso da reprodução: 0:01 de 0:00")).assertCountEquals(0)
        compose.onNodeWithText("Canal ao vivo 0").assertIsDisplayed()
        compose.onNodeWithText("Canal ao vivo 1").assertIsDisplayed()
    }

    @Test
    fun non_seekable_session_hides_seek_actions_but_keeps_basic_controls() {
        compose.setContent {
            RemotePlaybackSessionCard(
                session = RemotePlaybackSession("tv", "Sala", "Android TV", "Filme teste", true, false, 0L),
                busy = false,
                onToggle = {},
                onSeek = {},
                onStop = {},
            )
        }

        compose.onNodeWithText("Retomar").assertIsDisplayed()
        compose.onAllNodes(hasContentDescription("Voltar 30 segundos")).assertCountEquals(0)
        compose.onAllNodes(hasContentDescription("Avançar 30 segundos")).assertCountEquals(0)
        compose.onNodeWithContentDescription("Parar reprodução").assertIsDisplayed()
    }

    @Test
    fun screen_loads_remote_session_and_confirms_before_stopping_playback() {
        val repository = FakeRemotePlaybackRepository(listOf(
            RemotePlaybackSession("tv", "Sala", "Android TV", "Filme na TV", false, false, 0L),
        ))
        val viewModel = RemotePlaybackViewModel(repository)
        compose.setContent { RemotePlaybackScreen(onBack = {}, viewModel = viewModel) }

        compose.waitUntil(5_000) { repository.loadCount > 0 }
        compose.onNodeWithText("Filme na TV").assertIsDisplayed()
        compose.onNodeWithContentDescription("Parar reprodução").performClick()
        compose.onNodeWithText("Parar reprodução?").assertIsDisplayed()
        compose.onNodeWithText("Cancelar").performClick()
        compose.onAllNodes(hasText("Parar reprodução?")).assertCountEquals(0)
    }

    @Test
    fun failed_refresh_dismisses_stale_stop_confirmation_without_sending_stop() {
        val repository = FakeRemotePlaybackRepository(listOf(
            RemotePlaybackSession("tv", "Sala", "Android TV", "Filme na TV", false, false, 0L),
        ))
        val viewModel = RemotePlaybackViewModel(repository)
        compose.setContent { RemotePlaybackScreen(onBack = {}, viewModel = viewModel) }

        compose.waitUntil(5_000) { repository.loadCount > 0 }
        compose.onNodeWithContentDescription("Parar reprodução").performClick()
        compose.onNodeWithText("Parar reprodução?").assertIsDisplayed()

        repository.sessionsResult = Result.failure(IllegalStateException("Sessão encerrou"))
        viewModel.refresh()

        compose.waitUntil(5_000) { viewModel.state.value.error == "Sessão encerrou" }
        compose.onAllNodes(hasText("Parar reprodução?")).assertCountEquals(0)
        compose.runOnIdle { assertEquals(emptyList<Pair<String, RemotePlaybackCommand>>(), repository.commands) }
    }

    private class FakeRemotePlaybackRepository(
        private val sessions: List<RemotePlaybackSession>,
    ) : RemotePlaybackRepository {
        var loadCount = 0
        var sessionsResult: Result<List<RemotePlaybackSession>> = Result.success(sessions)
        val commands = mutableListOf<Pair<String, RemotePlaybackCommand>>()
        override suspend fun getActiveSessions(): Result<List<RemotePlaybackSession>> {
            loadCount++
            return sessionsResult
        }
        override suspend fun sendCommand(
            sessionId: String,
            command: RemotePlaybackCommand,
            seekPositionTicks: Long?,
        ): Result<Unit> {
            commands += sessionId to command
            return Result.success(Unit)
        }
    }
}
