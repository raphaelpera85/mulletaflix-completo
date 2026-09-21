package org.mulletaflix.feature.livetv

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.withContext
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.LiveTvRepository
import org.mulletaflix.domain.usecase.GetLiveTvChannelsUseCase

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)

class LiveTvViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var repository: FakeLiveTvRepository

    @Before fun setUp() { Dispatchers.setMain(dispatcher); repository = FakeLiveTvRepository() }
    @After fun tearDown() { Dispatchers.resetMain() }

    @Test fun `loads channels for active session`() = runTest {
        val channel = MediaItem(id = "channel-1", name = "Canal teste", type = org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)
        val viewModel = createViewModel()
        advanceUntilIdle()
        assertEquals(listOf(channel), viewModel.state.value.channels)
        assertTrue(viewModel.state.value.error == null)
    }

    @Test fun `does not request channels without a session`() = runTest {
        val viewModel = createViewModel(session = FakeSessionRepository(userId = null))
        advanceUntilIdle()
        assertTrue(viewModel.state.value.error!!.contains("Sessão expirada"))
        assertEquals(0, repository.channelRequests)
    }

    @Test fun `does not refresh live tv while offline and refreshes after reconnect`() = runTest {
        val network = FakeNetworkMonitor()
        val viewModel = createViewModel(networkMonitor = network)
        advanceUntilIdle()
        val requestsBeforeOffline = repository.channelRequests

        network.online.value = false
        advanceUntilIdle()
        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(requestsBeforeOffline, repository.channelRequests)
        assertTrue(viewModel.state.value.error?.contains("offline") == true)

        network.online.value = true
        advanceUntilIdle()

        assertEquals(requestsBeforeOffline + 1, repository.channelRequests)
        assertEquals(null, viewModel.state.value.error)
    }

    @Test fun `loads recordings for active session`() = runTest {
        val recording = MediaItem(id = "recording-1", name = "Jornal", type = org.mulletaflix.domain.model.MediaItemType.Recording)
        repository.recordings = listOf(recording)
        val viewModel = createViewModel()
        advanceUntilIdle()
        assertEquals(listOf(recording), viewModel.state.value.recordings)
        assertEquals(1, repository.recordingRequests)
    }

    @Test fun `schedules a guide program and marks it as scheduled`() = runTest {
        val program = MediaItem(
            id = "program-1",
            name = "Filme teste",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            channelId = "channel-1",
            startDate = "2026-09-14T20:00:00Z",
            endDate = "2026-09-14T22:00:00Z",
        )
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.scheduleRecording(program)
        advanceUntilIdle()

        assertEquals(listOf("program-1"), repository.scheduledIds)
        assertTrue("program-1" in viewModel.state.value.scheduledProgramIds)
    }

    @Test fun `repeated taps schedule a program only once`() = runTest {
        val program = MediaItem(
            id = "program-duplicate",
            name = "Filme teste",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            channelId = "channel-1",
            startDate = "2026-09-14T20:00:00Z",
            endDate = "2026-09-14T22:00:00Z",
        )
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.scheduleRecording(program)
        viewModel.scheduleRecording(program)
        advanceUntilIdle()

        assertEquals(listOf("program-duplicate"), repository.scheduledIds)
    }

    @Test fun `different programs can be scheduled without cancelling each other`() = runTest {
        val first = testProgram("program-first")
        val second = testProgram("program-second")
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.scheduleRecording(first)
        viewModel.scheduleRecording(second)
        advanceUntilIdle()

        assertEquals(listOf("program-first", "program-second"), repository.scheduledIds)
        assertEquals(setOf("program-first", "program-second"), viewModel.state.value.scheduledProgramIds)
    }

    @Test fun `late guide response cannot replace a newer guide request`() = runTest {
        val first = testProgram("guide-first")
        val second = testProgram("guide-second")
        val firstResponse = CompletableDeferred<Result<List<MediaItem>>>()
        val secondResponse = CompletableDeferred<Result<List<MediaItem>>>()
        repository.guideResponses.add(firstResponse)
        repository.guideResponses.add(secondResponse)
        repository.channels = listOf(MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel))
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadGuide()
        runCurrent()
        viewModel.loadGuide()
        runCurrent()
        secondResponse.complete(Result.success(listOf(second)))
        advanceUntilIdle()
        firstResponse.complete(Result.success(listOf(first)))
        advanceUntilIdle()

        assertEquals(listOf(second), viewModel.state.value.programs)
    }

    @Test fun `late guide response from a previous user cannot replace current session`() = runTest {
        val oldResponse = CompletableDeferred<Result<List<MediaItem>>>()
        repository.channels = listOf(MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel))
        repository.guideResponses.add(oldResponse)
        val session = FakeSessionRepository()
        val viewModel = createViewModel(session = session)
        advanceUntilIdle()

        viewModel.loadGuide()
        runCurrent()
        session.userIdState.value = "user-2"
        runCurrent()
        advanceUntilIdle()

        oldResponse.complete(Result.success(listOf(testProgram("stale-program"))))
        advanceUntilIdle()

        assertTrue(viewModel.state.value.programs.isEmpty())
        assertTrue(viewModel.state.value.channels.isNotEmpty())
    }

    private fun testProgram(id: String) = MediaItem(
        id = id,
        name = "Filme teste",
        type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
        channelId = "channel-1",
        startDate = "2026-09-14T20:00:00Z",
        endDate = "2026-09-14T22:00:00Z",
    )

    private fun createViewModel(
        session: FakeSessionRepository = FakeSessionRepository(),
        networkMonitor: FakeNetworkMonitor = FakeNetworkMonitor(),
    ) = LiveTvViewModel(GetLiveTvChannelsUseCase(repository), repository, session, networkMonitor)

    private class FakeLiveTvRepository : LiveTvRepository {
        var channels = emptyList<MediaItem>()
        var recordings = emptyList<MediaItem>()
        var channelRequests = 0
        var recordingRequests = 0
        val scheduledIds = mutableListOf<String>()
        val guideResponses = mutableListOf<CompletableDeferred<Result<List<MediaItem>>>>()
        override suspend fun getChannels(userId: String): Result<List<MediaItem>> { channelRequests++; return Result.success(channels) }
        override suspend fun getPrograms(channelIds: List<String>, minStartDate: String?, maxEndDate: String?): Result<List<MediaItem>> =
            withContext(NonCancellable) {
                guideResponses.removeFirstOrNull()?.await() ?: Result.success(emptyList())
            }
        override suspend fun getRecordings(userId: String): Result<List<MediaItem>> { recordingRequests++; return Result.success(recordings) }
        override suspend fun scheduleRecording(program: MediaItem): Result<Unit> {
            scheduledIds += program.id
            return Result.success(Unit)
        }
    }

    private class FakeSessionRepository(private val userId: String? = "user-1") : SessionRepository {
        val userIdState = kotlinx.coroutines.flow.MutableStateFlow(userId)
        override fun getAccessToken() = flowOf(null)
        override fun getDeviceId() = flowOf("test-device")
        override fun getBaseUrl() = flowOf("http://localhost:8096")
        override fun getCurrentUserId() = userIdState
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private class FakeNetworkMonitor : NetworkMonitor {
        val online = kotlinx.coroutines.flow.MutableStateFlow(true)
        override val isOnline = online
    }
}
