package org.mulletaflix.feature.livetv

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.LiveTvRepository

class LiveTvViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var repository: FakeLiveTvRepository

    @Before fun setUp() { Dispatchers.setMain(dispatcher); repository = FakeLiveTvRepository() }
    @After fun tearDown() { Dispatchers.resetMain() }

    @Test fun `loads channels for active session`() = runTest {
        val channel = MediaItem(id = "channel-1", name = "Canal teste", type = org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)
        val viewModel = LiveTvViewModel(repository, FakeSessionRepository())
        advanceUntilIdle()
        assertEquals(listOf(channel), viewModel.state.value.channels)
        assertTrue(viewModel.state.value.error == null)
    }

    @Test fun `does not request channels without a session`() = runTest {
        val viewModel = LiveTvViewModel(repository, FakeSessionRepository(userId = null))
        advanceUntilIdle()
        assertTrue(viewModel.state.value.error!!.contains("Sessão expirada"))
        assertEquals(0, repository.channelRequests)
    }

    private class FakeLiveTvRepository : LiveTvRepository {
        var channels = emptyList<MediaItem>()
        var channelRequests = 0
        override suspend fun getChannels(userId: String): Result<List<MediaItem>> { channelRequests++; return Result.success(channels) }
        override suspend fun getPrograms(channelIds: List<String>, minStartDate: String?, maxEndDate: String?) = Result.success(emptyList<MediaItem>())
        override suspend fun getRecordings(userId: String) = Result.success(emptyList<MediaItem>())
    }

    private class FakeSessionRepository(private val userId: String? = "user-1") : SessionRepository {
        override fun getAccessToken() = flowOf(null)
        override fun getDeviceId() = flowOf("test-device")
        override fun getBaseUrl() = flowOf("http://localhost:8096")
        override fun getCurrentUserId() = flowOf(userId)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
