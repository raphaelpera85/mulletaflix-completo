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

        viewModel.scheduleRecording(testProgram("schedule-without-session"))
        advanceUntilIdle()

        assertTrue(viewModel.state.value.schedulingProgramIds.isEmpty())
        assertTrue(viewModel.state.value.recordingActionError!!.contains("Sessão expirada"))
        assertTrue(repository.scheduledIds.isEmpty())
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

    @Test fun `reopening the guide retries timer lookup after schedule reconciliation fails`() = runTest {
        val program = testProgram("program-timer-retry")
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.scheduledLookupResponses += Result.success(emptyMap())
        repository.scheduledLookupResponses += Result.failure(IllegalStateException("indisponível"))
        repository.scheduledLookupResponses += Result.success(emptyMap())
        repository.scheduledLookupResponses += Result.failure(IllegalStateException("ainda não propagado"))
        repository.scheduledLookupResponses += Result.success(mapOf(program.id to "timer-retry"))
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.scheduleRecording(program)
        advanceUntilIdle()
        assertTrue(program.id in viewModel.state.value.scheduledProgramIds)
        assertTrue(program.id !in viewModel.state.value.scheduledProgramTimerIds)

        viewModel.loadGuide()
        advanceUntilIdle()

        assertEquals("timer-retry", viewModel.state.value.scheduledProgramTimerIds[program.id])
    }

    @Test fun `successful schedule retries delayed timer lookup and manual retry resolves without reopening guide`() = runTest {
        val program = testProgram("program-delayed-timer")
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        val viewModel = createViewModel()
        advanceUntilIdle()
        val lookupsBeforeSchedule = repository.scheduledRequests

        repository.scheduledLookupResponses += Result.failure(IllegalStateException("temporarily unavailable"))
        repository.scheduledLookupResponses += Result.success(emptyMap())
        repository.scheduledLookupResponses += Result.failure(IllegalStateException("not propagated yet"))
        viewModel.scheduleRecording(program)
        advanceUntilIdle()

        assertTrue(program.id in viewModel.state.value.scheduledProgramIds)
        assertTrue(program.id in viewModel.state.value.locallyScheduledProgramIds)
        assertTrue(program.id !in viewModel.state.value.scheduledProgramTimerIds)
        assertTrue(viewModel.state.value.resolvingTimerProgramIds.isEmpty())
        assertEquals(lookupsBeforeSchedule + 3, repository.scheduledRequests)

        repository.scheduledLookupResponses += Result.success(mapOf(program.id to "timer-delayed"))
        viewModel.retryScheduledRecordingTimerLookup(program)
        advanceUntilIdle()

        assertEquals("timer-delayed", viewModel.state.value.scheduledProgramTimerIds[program.id])
        assertTrue(program.id !in viewModel.state.value.locallyScheduledProgramIds)
        assertEquals(lookupsBeforeSchedule + 4, repository.scheduledRequests)
    }

    @Test fun `late timer lookup cannot update guide after network drops`() = runTest {
        val program = testProgram("program-network-drop")
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        val network = FakeNetworkMonitor()
        val viewModel = createViewModel(networkMonitor = network)
        advanceUntilIdle()
        val pendingLookup = CompletableDeferred<Result<Map<String, String>>>()
        repository.scheduledLookupDeferredResponses += pendingLookup

        viewModel.scheduleRecording(program)
        runCurrent()
        assertTrue(program.id in viewModel.state.value.resolvingTimerProgramIds)

        network.online.value = false
        runCurrent()
        pendingLookup.complete(Result.success(mapOf(program.id to "timer-stale")))
        advanceUntilIdle()

        assertTrue(program.id !in viewModel.state.value.scheduledProgramTimerIds)
        assertTrue(program.id in viewModel.state.value.locallyScheduledProgramIds)
        assertTrue(viewModel.state.value.resolvingTimerProgramIds.isEmpty())
    }

    @Test fun `guide refresh removes a timer cancelled outside the app`() = runTest {
        val program = testProgram("program-cancelled-remotely")
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.scheduledResponse = Result.success(mapOf(program.id to "timer-remote"))
        val viewModel = createViewModel()
        advanceUntilIdle()
        assertTrue(program.id in viewModel.state.value.scheduledProgramIds)

        repository.scheduledLookupResponses += Result.success(emptyMap())
        viewModel.loadGuide()
        advanceUntilIdle()

        assertTrue(program.id !in viewModel.state.value.scheduledProgramIds)
        assertTrue(program.id !in viewModel.state.value.scheduledProgramTimerIds)
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

    @Test fun `cancels a scheduled recording and clears its guide state`() = runTest {
        val program = testProgram("program-cancel")
        repository.scheduledResponse = Result.success(mapOf(program.id to "timer-cancel"))
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.cancelScheduledRecording(program)
        advanceUntilIdle()

        assertEquals(listOf("timer-cancel"), repository.cancelledTimerIds)
        assertTrue(program.id !in viewModel.state.value.scheduledProgramIds)
        assertTrue(program.id !in viewModel.state.value.scheduledProgramTimerIds)
        assertTrue(program.id !in viewModel.state.value.cancellingProgramIds)
    }

    @Test fun `failed timer cancellation keeps the programme scheduled without a misleading guide retry`() = runTest {
        val program = testProgram("program-cancel-failure")
        repository.scheduledResponse = Result.success(mapOf(program.id to "timer-fail"))
        repository.cancellationResult = Result.failure(IllegalStateException("Permissão negada"))
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.cancelScheduledRecording(program)
        advanceUntilIdle()

        assertTrue(program.id in viewModel.state.value.scheduledProgramIds)
        assertTrue(program.id in viewModel.state.value.scheduledProgramTimerIds)
        assertTrue(viewModel.state.value.cancellingProgramIds.isEmpty())
        assertEquals("Permissão negada", viewModel.state.value.recordingActionError)
        assertEquals(null, viewModel.state.value.guideError)
    }

    @Test fun `offline cancellation does not call the server`() = runTest {
        val program = testProgram("program-cancel-offline")
        repository.scheduledResponse = Result.success(mapOf(program.id to "timer-offline"))
        val network = FakeNetworkMonitor()
        val viewModel = createViewModel(networkMonitor = network)
        advanceUntilIdle()
        network.online.value = false
        advanceUntilIdle()

        viewModel.cancelScheduledRecording(program)
        advanceUntilIdle()

        assertTrue(repository.cancelledTimerIds.isEmpty())
        assertTrue(program.id in viewModel.state.value.scheduledProgramIds)
        assertTrue(viewModel.state.value.recordingActionError!!.contains("offline"))
    }

    @Test fun `cancellation does not reach the server if the session changes before the request starts`() = runTest {
        val program = testProgram("program-cancel-session-race")
        repository.scheduledResponse = Result.success(mapOf(program.id to "timer-session-race"))
        val session = FakeSessionRepository()
        val viewModel = createViewModel(session = session)
        advanceUntilIdle()

        viewModel.cancelScheduledRecording(program)
        session.userIdState.value = "user-2"
        advanceUntilIdle()

        assertTrue(repository.cancelledTimerIds.isEmpty())
    }

    @Test fun `cancellation rechecks connectivity immediately before the request`() = runTest {
        val program = testProgram("program-cancel-network-race")
        repository.scheduledResponse = Result.success(mapOf(program.id to "timer-network-race"))
        val network = FakeNetworkMonitor()
        val viewModel = createViewModel(networkMonitor = network)
        advanceUntilIdle()

        viewModel.cancelScheduledRecording(program)
        network.online.value = false
        advanceUntilIdle()

        assertTrue(repository.cancelledTimerIds.isEmpty())
        assertTrue(program.id !in viewModel.state.value.cancellingProgramIds)
        assertTrue(viewModel.state.value.recordingActionError!!.contains("offline"))
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

    @Test fun `guide load waits for active channel refresh and runs once on the settled snapshot`() = runTest {
        val inFlight = CompletableDeferred<Result<List<MediaItem>>>()
        val channel = MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)
        repository.channelResponses.add(inFlight)
        val viewModel = createViewModel()

        runCurrent()
        viewModel.loadGuide()
        runCurrent()

        assertEquals("the EPG must wait while channel IDs are being refreshed", 0, repository.guideRequests)
        inFlight.complete(Result.success(listOf(channel)))
        advanceUntilIdle()

        assertEquals("the settled channel snapshot should trigger exactly one EPG request", 1, repository.guideRequests)
    }

    @Test fun `idle refresh sees the active job before loading state is published`() = runTest {
        // The foreground effect can run in the same frame as the session
        // collector. At that point isLoading is still false, but refreshJob
        // already owns the initial request and must prevent a replacement.
        val inFlight = CompletableDeferred<Result<List<MediaItem>>>()
        val channel = MediaItem("channel-race", "Canal corrida", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel)
        repository.channels = listOf(channel)
        repository.channelResponses.add(inFlight)
        repository.channelResponses.add(CompletableDeferred())
        val viewModel = createViewModel()

        viewModel.refreshIfIdle()
        runCurrent()

        inFlight.complete(Result.success(listOf(channel)))
        advanceUntilIdle()

        assertEquals(1, repository.channelRequests)
        assertEquals(listOf(channel), viewModel.state.value.channels)
    }

    @Test fun `programmes already scheduled on the server are marked as scheduled`() = runTest {
        // Reported as a real risk: without this the guide had no idea what was
        // already recording, so reopening the screen showed "Gravar" for a
        // programme that was set to record and tapping it created a second timer.
        repository.channels = listOf(
            MediaItem("channel-1", "Canal", org.mulletaflix.domain.model.MediaItemType.LiveTvChannel),
        )
        repository.scheduledResponse = Result.success(mapOf("prog-1" to "timer-1", "prog-2" to "timer-2"))

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
        repository.scheduledResponse = Result.success(mapOf("prog-duplicate" to "timer-duplicate"))
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
        var scheduledResponse: Result<Map<String, String>> = Result.success(emptyMap())
        val scheduledLookupResponses = mutableListOf<Result<Map<String, String>>>()
        val scheduledLookupDeferredResponses = mutableListOf<CompletableDeferred<Result<Map<String, String>>>>()
        var cancellationResult: Result<Unit> = Result.success(Unit)
        val scheduledIds = mutableListOf<String>()
        val cancelledTimerIds = mutableListOf<String>()
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
        override suspend fun getScheduledProgramTimerIds(): Result<Map<String, String>> {
            scheduledRequests++
            scheduledLookupDeferredResponses.removeFirstOrNull()?.let { return it.await() }
            scheduledLookupResponses.removeFirstOrNull()?.let { return it }
            return scheduledResponse
        }
        override suspend fun scheduleRecording(program: MediaItem): Result<Unit> {
            scheduledIds += program.id
            scheduledResponse = Result.success(scheduledResponse.getOrDefault(emptyMap()) + (program.id to "timer-${program.id}"))
            return Result.success(Unit)
        }
        override suspend fun cancelScheduledRecording(timerId: String): Result<Unit> {
            cancelledTimerIds += timerId
            return cancellationResult
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
