package org.mulletaflix.feature.syncplay

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayRepository
import org.mulletaflix.domain.repository.SyncPlayPlaybackCommand

import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.core.api.SavedServerSession
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.SyncPlayRealtimeEvent

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class SyncPlayViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before fun setUp() = Dispatchers.setMain(dispatcher)
    @After fun tearDown() = Dispatchers.resetMain()

    @Test fun `realtime notifications distinguish repeated events`() {
        val first = syncPlayRealtimeNotification(SyncPlayRealtimeEvent.QueueUpdate("g1", "item-1", null, 0L, true))
        val second = syncPlayRealtimeNotification(SyncPlayRealtimeEvent.Disconnected)

        assertEquals("Mídia da sala atualizada", first)
        assertEquals("Sincronização desconectada; tentando reconectar…", second)
    }

    @Test fun `realtime content events are limited to the active room`() {
        val matchingCommand = SyncPlayRealtimeEvent.Command("room-active", "item", "Pause", 0L)
        val otherRoomUpdate = SyncPlayRealtimeEvent.GroupUpdate("room-other", "GroupJoined")
        val matchingQueue = SyncPlayRealtimeEvent.QueueUpdate("room-active", "item", null, 0L, true)

        assertEquals(true, shouldHandleSyncPlayRealtimeEvent(matchingCommand, "room-active"))
        assertEquals(false, shouldHandleSyncPlayRealtimeEvent(otherRoomUpdate, "room-active"))
        assertEquals(true, shouldHandleSyncPlayRealtimeEvent(matchingQueue, "room-active"))
        assertEquals(false, shouldHandleSyncPlayRealtimeEvent(matchingCommand, null))
    }

    @Test fun `realtime connection events are ignored when there is no active room`() {
        assertEquals(false, shouldHandleSyncPlayRealtimeEvent(SyncPlayRealtimeEvent.Connected, null))
        assertEquals(false, shouldHandleSyncPlayRealtimeEvent(SyncPlayRealtimeEvent.Disconnected, ""))
        assertEquals(true, shouldHandleSyncPlayRealtimeEvent(SyncPlayRealtimeEvent.Connected, "room-active"))
    }

    @Test fun `refresh exposes current groups`() = runTest {
        val groups = listOf(SyncPlayGroup("g1", "Filme", "Playing", listOf("Raphael")))
        val repository = FakeRepository(groups)
        val useCase = ManageSyncPlayUseCase(repository)
        val viewModel = SyncPlayViewModel(useCase, repository)

        advanceUntilIdle()

        assertEquals(groups, viewModel.state.value.groups)
        assertEquals(1, repository.listCalls)
    }

    @Test fun `joining a group marks it active`() = runTest {
        val group = SyncPlayGroup("g1", "Filme", "Paused", emptyList())
        val repository = FakeRepository(listOf(group))
        val useCase = ManageSyncPlayUseCase(repository)
        val viewModel = SyncPlayViewModel(useCase, repository)
        advanceUntilIdle()

        viewModel.joinGroup("g1")
        advanceUntilIdle()

        assertEquals("g1", viewModel.state.value.activeGroupId)
    }

    @Test fun `late groups from a previous user cannot replace the current session`() = runTest {
        val staleGroups = CompletableDeferred<Result<List<SyncPlayGroup>>>()
        val userId = MutableStateFlow<String?>("user-1")
        val repository = FakeRepository(emptyList(), staleGroups, userId)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()

        userId.value = "user-2"
        advanceUntilIdle()
        staleGroups.complete(Result.success(listOf(SyncPlayGroup("old", "Conta antiga", "Paused", emptyList()))))
        advanceUntilIdle()

        assertEquals(emptyList<SyncPlayGroup>(), viewModel.state.value.groups)
        assertEquals(2, repository.listCalls)
    }

    /**
     * O botão desabilitado é lido na composição, então dois toques no mesmo frame
     * passam os dois. Sem a guarda síncrona o servidor recebia dois pedidos: duas
     * salas com o mesmo nome, ou duas entradas na mesma sala.
     *
     * A janela é real e é a mesma que já produziu dois downloads do mesmo arquivo
     * em Ajustes (v1.2.70): o flag era marcado dentro da corrotina, que só começa
     * depois do segundo toque.
     */
    @Test fun `two taps in the same frame create only one room`() = runTest {
        val gate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRepository(emptyList(), createGate = gate)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()

        viewModel.createGroup("Sala")
        viewModel.createGroup("Sala")

        gate.complete(Result.success(Unit))
        advanceUntilIdle()

        assertEquals(1, repository.createCalls)
    }

    @Test fun `two taps in the same frame join the room once`() = runTest {
        val group = SyncPlayGroup("g1", "Filme", "Paused", emptyList())
        val gate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRepository(listOf(group), joinGate = gate)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()

        viewModel.joinGroup("g1")
        viewModel.joinGroup("g1")

        gate.complete(Result.success(Unit))
        advanceUntilIdle()

        assertEquals(1, repository.joinCalls)
    }

    @Test fun `two taps in the same frame leave the room once`() = runTest {
        val gate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRepository(emptyList(), leaveGate = gate)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()

        viewModel.leaveGroup()
        viewModel.leaveGroup()

        gate.complete(Result.success(Unit))
        advanceUntilIdle()

        assertEquals(1, repository.leaveCalls)
    }

    @Test fun `a rejected second tap does not leave the screen stuck`() = runTest {
        val gate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRepository(emptyList(), createGate = gate)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()

        viewModel.createGroup("Sala")
        viewModel.createGroup("Sala")
        gate.complete(Result.success(Unit))
        advanceUntilIdle()

        // A guarda não pode ser um cadeado: depois da resposta o próximo envio volta.
        assertEquals(false, viewModel.state.value.isSubmitting)
        viewModel.createGroup("Outra")
        advanceUntilIdle()
        assertEquals(2, repository.createCalls)
    }

    @Test fun `cancelled room creation releases submission guard for retry`() = runTest {
        val repository = FakeRepository(emptyList(), cancelFirstCreate = true)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()

        viewModel.createGroup("Sala")
        advanceUntilIdle()
        assertEquals(false, viewModel.state.value.isSubmitting)

        viewModel.createGroup("Sala")
        advanceUntilIdle()

        assertEquals(2, repository.createCalls)
        assertEquals(false, viewModel.state.value.isSubmitting)
    }

    @Test fun `playback command is sent only once while submitting`() = runTest {
        val gate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRepository(listOf(SyncPlayGroup("g1", "Filme", "Playing", emptyList())), commandGate = gate)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()
        viewModel.joinGroup("g1")
        advanceUntilIdle()
        viewModel.sendPlaybackCommand(SyncPlayPlaybackCommand.PAUSE)
        viewModel.sendPlaybackCommand(SyncPlayPlaybackCommand.PAUSE)
        gate.complete(Result.success(Unit))
        advanceUntilIdle()
        assertEquals(1, repository.commandCalls)
    }

    private class FakeRepository(
        private val groups: List<SyncPlayGroup>,
        private val firstResponse: CompletableDeferred<Result<List<SyncPlayGroup>>>? = null,
        private val userId: MutableStateFlow<String?> = MutableStateFlow("user-1"),
        private val createGate: CompletableDeferred<Result<Unit>>? = null,
        private val joinGate: CompletableDeferred<Result<Unit>>? = null,
        private val leaveGate: CompletableDeferred<Result<Unit>>? = null,
        private val commandGate: CompletableDeferred<Result<Unit>>? = null,
        private val cancelFirstCreate: Boolean = false,
    ) : SyncPlayRepository, SessionRepository {
        var listCalls = 0
        var createCalls = 0
        var joinCalls = 0
        var leaveCalls = 0
        var commandCalls = 0
        override suspend fun getGroups(): Result<List<SyncPlayGroup>> {
            listCalls++
            if (listCalls == 1 && firstResponse != null) return firstResponse.await()
            return Result.success(groups)
        }
        override suspend fun createGroup(name: String): Result<Unit> {
            createCalls++
            if (cancelFirstCreate && createCalls == 1) throw CancellationException("request cancelled")
            return createGate?.await() ?: Result.success(Unit)
        }
        override suspend fun joinGroup(groupId: String): Result<Unit> {
            joinCalls++
            return joinGate?.await() ?: Result.success(Unit)
        }
        override suspend fun leaveGroup(): Result<Unit> {
            leaveCalls++
            return leaveGate?.await() ?: Result.success(Unit)
        }
        override suspend fun sendPlaybackCommand(command: SyncPlayPlaybackCommand): Result<Unit> {
            commandCalls++
            return commandGate?.await() ?: Result.success(Unit)
        }
        override suspend fun reportBuffering(status: org.mulletaflix.domain.repository.SyncPlayPlaybackStatus) = Result.success(Unit)
        override suspend fun reportReady(status: org.mulletaflix.domain.repository.SyncPlayPlaybackStatus) = Result.success(Unit)
        override fun getAccessToken(): Flow<String?> = flowOf(null)
        override fun getDeviceId(): Flow<String> = flowOf("device")
        override fun getBaseUrl(): Flow<String> = flowOf("http://server")
        override fun getCurrentUserId(): Flow<String?> = userId
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
