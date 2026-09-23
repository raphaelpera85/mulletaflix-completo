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

    @Test fun `channel refresh does not leave the guide loading forever`() = runTest {
        // The channel timer (every 60 s) and the RESUMED transition both call
        // refresh(), which bumps guideGeneration to invalidate the guide in
        // flight. That invalidated request returns before clearing its own
        // flag, so refresh() has to clear it. Without that, the EPG dialog
        // spins forever and the "Guia EPG" action stays disabled.
        val guideResponse = CompletableDeferred<Result<List<MediaItem>>>()
        repository.channels = listOf(MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel))
        repository.guideResponses.add(guideResponse)
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadGuide()
        runCurrent()
        assertTrue("expected the guide to be loading", viewModel.state.value.isLoadingGuide)

        viewModel.refresh()
        runCurrent()

        assertTrue(
            "refresh() must not leave the guide flag stuck while a request is in flight",
            !viewModel.state.value.isLoadingGuide,
        )

        guideResponse.complete(Result.success(listOf(testProgram("stale-guide"))))
        advanceUntilIdle()

        assertTrue(
            "the invalidated guide response must not be applied",
            viewModel.state.value.programs.isEmpty(),
        )
        assertTrue(
            "the guide must still be loadable after the response lands",
            !viewModel.state.value.isLoadingGuide,
        )
    }

    @Test fun `idle refresh does not cancel an in flight channel request`() = runTest {
        // The TV foreground timer must not tear down a request it did not
        // start; that is the whole point of refreshIfIdle().
        val inFlight = CompletableDeferred<Result<List<MediaItem>>>()
        val channel = MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)
        repository.channelResponses.add(inFlight)
        repository.channelResponses.add(CompletableDeferred())
        val viewModel = createViewModel()

        runCurrent()
        assertTrue("expected the initial channel request to be in flight", repository.channelResponses.isNotEmpty())

        viewModel.refreshIfIdle()
        runCurrent()

        inFlight.complete(Result.success(listOf(channel)))
        advanceUntilIdle()

        assertEquals(listOf(channel), viewModel.state.value.channels)
        assertEquals(
            "refreshIfIdle must not issue a second channel request while one is loading",
            1,
            repository.channelRequests,
        )
    }

    @Test fun `programmes already scheduled on the server are marked as scheduled`() = runTest {
        // Reported as a real risk: without this the guide had no idea what was
        // already recording, so reopening the screen showed "Gravar" for a
        // programme that was set to record and tapping it created a second timer.
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.scheduledResponse = Result.success(setOf("prog-1", "prog-2"))

        val viewModel = createViewModel()
        advanceUntilIdle()

        assertEquals(setOf("prog-1", "prog-2"), viewModel.state.value.scheduledProgramIds)
        assertTrue(
            "the server must be asked what is already scheduled",
            repository.scheduledRequests >= 1,
        )
    }

    @Test fun `a programme already scheduled remotely is never scheduled twice`() = runTest {
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.scheduledResponse = Result.success(setOf("prog-duplicate"))
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.scheduleRecording(testProgram("prog-duplicate"))
        advanceUntilIdle()

        assertEquals(
            "the server already has this recording; a second timer must not be created",
            emptyList<String>(),
            repository.scheduledIds,
        )
    }

    @Test fun `a failed scheduled lookup keeps the session marks and still loads channels`() = runTest {
        // Not knowing what is scheduled must never break the channel list, and
        // must never undo a mark this session already earned.
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.scheduledResponse = Result.failure(IllegalStateException("indisponível"))
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.scheduleRecording(testProgram("prog-local"))
        advanceUntilIdle()
        assertEquals(listOf("prog-local"), repository.scheduledIds)

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(
            "a failed lookup must not erase what this session already scheduled",
            setOf("prog-local"),
            viewModel.state.value.scheduledProgramIds,
        )
        assertTrue(viewModel.state.value.error == null)
    }

    @Test fun `a refresh reloads an open guide instead of stranding it empty`() = runTest {
        // The dialog was open and its guide still loading when a refresh landed.
        // `refresh()` invalidates the guide request and clears the loading flag,
        // so the dialog sat on "Nenhum programa encontrado para as próximas 24
        // horas." with no request behind it until the user closed and reopened it.
        val channel = MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)
        repository.guideResponses.add(CompletableDeferred())
        repository.guideResponses.add(
            CompletableDeferred(Result.success(listOf(testProgram("prog-after-refresh")))),
        )

        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadGuide()
        runCurrent()
        assertTrue("the guide must be in flight", viewModel.state.value.isLoadingGuide)

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(
            "the open dialog must get a fresh guide after a refresh",
            2,
            repository.guideRequests,
        )
        assertEquals(
            "the reloaded guide must reach the state the dialog reads",
            listOf(testProgram("prog-after-refresh")),
            viewModel.state.value.programs,
        )
        assertTrue(!viewModel.state.value.isLoadingGuide)
    }

    @Test fun `a failed recordings request is surfaced instead of looking empty`() = runTest {
        // A falha era engolida com `getOrDefault(emptyList())`: a seção de gravações
        // simplesmente desaparecia, indistinguível de "você não tem gravações".
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.recordingsResult = Result.failure(IllegalStateException("Servidor fora do ar"))

        val viewModel = createViewModel()
        advanceUntilIdle()

        assertEquals(
            "Servidor fora do ar",
            viewModel.state.value.recordingsError,
        )
        assertEquals(emptyList<MediaItem>(), viewModel.state.value.recordings)
        assertEquals("os canais continuam carregando", 1, viewModel.state.value.channels.size)
    }

    @Test fun `a successful recordings request clears the error`() = runTest {
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.recordingsResult = Result.failure(IllegalStateException("Servidor fora do ar"))
        val viewModel = createViewModel()
        advanceUntilIdle()
        assertTrue(viewModel.state.value.recordingsError != null)

        val recording = MediaItem("rec-1", "Jornal", org.mulletaflix.domain.model.MediaItemType.Recording)
        repository.recordingsResult = null
        repository.recordings = listOf(recording)
        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(null, viewModel.state.value.recordingsError)
        assertEquals(listOf(recording), viewModel.state.value.recordings)
    }

    @Test fun `the guide is requested for a 24 hour window`() = runTest {
        // A janela é de sobreposição: o repositório a traduz para `MinEndDate` +
        // `MaxStartDate`, que é o que inclui o programa que está no ar agora. Aqui se
        // fixa o tamanho da janela, para uma mudança de filtro não encurtá-la sem
        // ninguém notar.
        val channel = MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)

        val viewModel = createViewModel()
        advanceUntilIdle()
        viewModel.loadGuide()
        advanceUntilIdle()

        val (startUtc, endUtc) = repository.guideWindows.single()
        val parser = java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", java.util.Locale.US).apply {
            timeZone = java.util.TimeZone.getTimeZone("UTC")
        }
        val start = parser.parse(startUtc!!)!!.time
        val end = parser.parse(endUtc!!)!!.time
        assertEquals(
            "o guia deve cobrir exatamente 24 horas",
            java.util.concurrent.TimeUnit.HOURS.toMillis(24),
            end - start,
        )
        assertTrue(
            "a janela precisa começar em volta de agora",
            kotlin.math.abs(start - System.currentTimeMillis()) < java.util.concurrent.TimeUnit.MINUTES.toMillis(5),
        )
    }

    @Test fun `a guide from a replaced channel snapshot is dropped`() = runTest {
        // O guia antigo descrevia canais que não estão mais na tela: com a nova
        // requisição falhando, o usuário podia agendar um programa de um canal que
        // sumiu do snapshot.
        val first = MediaItem("channel-1", "Canal 1", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        val second = MediaItem("channel-2", "Canal 2", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(first)
        repository.guideResponses.add(
            CompletableDeferred(Result.success(listOf(testProgram("prog-old")))),
        )
        val viewModel = createViewModel()
        advanceUntilIdle()
        viewModel.loadGuide()
        advanceUntilIdle()
        assertEquals(listOf(testProgram("prog-old")), viewModel.state.value.programs)

        // O refresh traz outro conjunto de canais e a nova requisição do guia falha.
        repository.channels = listOf(second)
        repository.guideResponses.add(CompletableDeferred(Result.failure(IllegalStateException("rede caiu"))))
        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(listOf(second), viewModel.state.value.channels)
        assertTrue(
            "programas de um snapshot de canais que já mudou não podem continuar na tela",
            viewModel.state.value.programs.isEmpty(),
        )
    }

    @Test fun `a closed guide is not reloaded by a refresh`() = runTest {
        val channel = MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadGuide()
        advanceUntilIdle()
        val loadsAfterOpen = repository.guideRequests

        viewModel.closeGuide()
        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(
            "a dismissed dialog must not keep issuing guide requests",
            loadsAfterOpen,
            repository.guideRequests,
        )
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

        /** Quando definido, a próxima leitura de gravações devolve este resultado. */
        var recordingsResult: Result<List<MediaItem>>? = null
        var channelRequests = 0
        var recordingRequests = 0
        var scheduledRequests = 0
        var guideRequests = 0
        var scheduledResponse: Result<Set<String>> = Result.success(emptySet())
        val scheduledIds = mutableListOf<String>()
        val guideResponses = mutableListOf<CompletableDeferred<Result<List<MediaItem>>>>()
        val channelResponses = mutableListOf<CompletableDeferred<Result<List<MediaItem>>>>()
        val guideWindows = mutableListOf<Pair<String?, String?>>()
        override suspend fun getChannels(userId: String): Result<List<MediaItem>> {
            channelRequests++
            return withContext(NonCancellable) {
                channelResponses.removeFirstOrNull()?.await() ?: Result.success(channels)
            }
        }
        override suspend fun getPrograms(channelIds: List<String>, windowStartUtc: String?, windowEndUtc: String?): Result<List<MediaItem>> {
            guideRequests++
            guideWindows += windowStartUtc to windowEndUtc
            val response = guideResponses.removeFirstOrNull()
            if (response == null) return Result.success(emptyList())
            // A transport that ignores cancellation still has to *finish* its
            // in-flight response; it just does not let the caller's cancel
            // stop work already on the wire. `withContext(NonCancellable)`
            // would be wrong here: it also swallows the cancellation of this
            // job, which no real client does, and it would hide the fact that
            // refresh() must clear the guide flag itself.
            return try {
                response.await()
            } catch (cancelled: kotlinx.coroutines.CancellationException) {
                response.complete(Result.success(emptyList()))
                throw cancelled
            }
        }
        override suspend fun getRecordings(userId: String): Result<List<MediaItem>> {
            recordingRequests++
            recordingsResult?.let { return it }
            return Result.success(recordings)
        }
        override suspend fun getScheduledProgramIds(): Result<Set<String>> {
            scheduledRequests++
            return scheduledResponse
        }
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
