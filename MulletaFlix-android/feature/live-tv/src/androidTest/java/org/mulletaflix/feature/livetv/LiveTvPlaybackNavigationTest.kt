package org.mulletaflix.feature.livetv

import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.LiveTvRepository
import org.mulletaflix.domain.usecase.GetLiveTvChannelsUseCase

/** Confirms that the TV playback actions forward the selected media ID. */
@RunWith(AndroidJUnit4::class)
class LiveTvPlaybackNavigationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun playingChannelForwardsChannelId() {
        assumeTelevisionProfile()
        val repository = PlaybackRepository(
            channels = listOf(media("channel-id-42", "Canal de teste", MediaItemType.LiveTvChannel)),
        )
        val viewModel = playbackViewModel(repository)
        var selectedId: String? = null

        composeRule.setContent {
            MulletaFlixTheme {
                LiveTvScreen(viewModel = viewModel, onChannelPlay = { selectedId = it })
            }
        }

        composeRule.waitUntil(timeoutMillis = 5_000) { composeRule.onAllNodesWithText("Canal de teste").fetchSemanticsNodes().isNotEmpty() }
        composeRule.onNodeWithContentDescription("Assistir Canal de teste").performClick()
        composeRule.runOnIdle { assertEquals("channel-id-42", selectedId) }
    }

    @Test
    fun playingRecordingForwardsRecordingId() {
        assumeTelevisionProfile()
        val repository = PlaybackRepository(
            recordings = listOf(media("recording-id-73", "Gravação de teste", MediaItemType.Recording)),
        )
        val viewModel = playbackViewModel(repository)
        var selectedId: String? = null

        composeRule.setContent {
            MulletaFlixTheme {
                LiveTvScreen(viewModel = viewModel, onChannelPlay = { selectedId = it })
            }
        }

        composeRule.waitUntil(timeoutMillis = 5_000) { composeRule.onAllNodesWithText("Gravação de teste").fetchSemanticsNodes().isNotEmpty() }
        composeRule.onNodeWithText("Gravação de teste").performClick()
        composeRule.runOnIdle { assertEquals("recording-id-73", selectedId) }
    }

    private fun playbackViewModel(repository: PlaybackRepository) = LiveTvViewModel(
        getLiveTvChannelsUseCase = GetLiveTvChannelsUseCase(repository),
        repository = repository,
        sessionRepository = PlaybackSessionRepository,
        networkMonitor = PlaybackNetworkMonitor,
    )

    private fun media(id: String, name: String, type: MediaItemType) = MediaItem(
        id = id,
        name = name,
        type = type,
    )

    private class PlaybackRepository(
        private val channels: List<MediaItem> = emptyList(),
        private val recordings: List<MediaItem> = emptyList(),
    ) : LiveTvRepository {
        override suspend fun getChannels(userId: String) = Result.success(channels)
        override suspend fun getPrograms(channelIds: List<String>, windowStartUtc: String?, windowEndUtc: String?) =
            Result.success(emptyList<MediaItem>())
        override suspend fun getRecordings(userId: String) = Result.success(recordings)
        override suspend fun scheduleRecording(program: MediaItem) = Result.success(Unit)
        override suspend fun getScheduledProgramTimerIds() = Result.success(emptyMap<String, String>())
        override suspend fun cancelScheduledRecording(timerId: String) = Result.success(Unit)
    }

    private object PlaybackSessionRepository : SessionRepository {
        override fun getAccessToken(): Flow<String?> = flowOf(null)
        override fun getDeviceId(): Flow<String> = flowOf("test-device")
        override fun getBaseUrl(): Flow<String> = flowOf("https://example.invalid")
        override fun getCurrentUserId(): Flow<String?> = flowOf("test-user")
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private object PlaybackNetworkMonitor : NetworkMonitor {
        override val isOnline: Flow<Boolean> = flowOf(true)
    }
}
