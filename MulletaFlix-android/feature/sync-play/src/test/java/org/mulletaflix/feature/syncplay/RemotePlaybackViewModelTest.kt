package org.mulletaflix.feature.syncplay

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackRepository
import org.mulletaflix.domain.repository.RemotePlaybackSession

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class RemotePlaybackViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before fun setUp() = Dispatchers.setMain(dispatcher)
    @After fun tearDown() = Dispatchers.resetMain()

    @Test
    fun `refresh publishes only sessions provided by repository`() = runTest {
        val expected = listOf(session())
        val viewModel = RemotePlaybackViewModel(FakeRemotePlaybackRepository(sessions = expected))

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(expected, viewModel.state.value.sessions)
        assertFalse(viewModel.state.value.isLoading)
        assertEquals(null, viewModel.state.value.error)
    }

    @Test
    fun `failed refresh clears stale sessions and exposes retryable error`() = runTest {
        val repository = FakeRemotePlaybackRepository(sessions = listOf(session()))
        val viewModel = RemotePlaybackViewModel(repository)
        viewModel.refresh()
        advanceUntilIdle()
        repository.sessionsResult = Result.failure(IllegalStateException("Servidor indisponível"))

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(emptyList<RemotePlaybackSession>(), viewModel.state.value.sessions)
        assertFalse(viewModel.state.value.isLoading)
        assertEquals("Servidor indisponível", viewModel.state.value.error)
    }

    @Test
    fun `command failure leaves sessions visible and exposes actionable error`() = runTest {
        val repository = FakeRemotePlaybackRepository(sessions = listOf(session()), commandResult = Result.failure(IllegalStateException("Dispositivo desconectado")))
        val viewModel = RemotePlaybackViewModel(repository)
        viewModel.refresh()
        advanceUntilIdle()

        viewModel.sendCommand("session-1", RemotePlaybackCommand.PLAY_PAUSE)
        advanceUntilIdle()

        assertEquals(1, viewModel.state.value.sessions.size)
        assertEquals("Dispositivo desconectado", viewModel.state.value.error)
        assertEquals(null, viewModel.state.value.busySessionId)
    }

    @Test
    fun `duplicate command is ignored while the first command is pending`() = runTest {
        val gate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRemotePlaybackRepository(commandGate = gate)
        val viewModel = RemotePlaybackViewModel(repository)

        viewModel.sendCommand("session-1", RemotePlaybackCommand.PLAY_PAUSE)
        runCurrent()
        viewModel.sendCommand("session-2", RemotePlaybackCommand.STOP)
        assertEquals("session-1", viewModel.state.value.busySessionId)
        assertEquals(listOf("session-1" to RemotePlaybackCommand.PLAY_PAUSE), repository.commands)

        gate.complete(Result.success(Unit))
        advanceUntilIdle()
        assertNotNull(viewModel.state.value.notice)
    }

    @Test
    fun `refresh requested during in-flight refresh runs after current response`() = runTest {
        val firstFetch = CompletableDeferred<Result<List<RemotePlaybackSession>>>()
        var fetchCount = 0
        val repository = object : RemotePlaybackRepository {
            override suspend fun getActiveSessions(): Result<List<RemotePlaybackSession>> {
                fetchCount += 1
                return if (fetchCount == 1) {
                    firstFetch.await()
                } else {
                    Result.success(listOf(session().copy(itemName = "Atualizado")))
                }
            }

            override suspend fun sendCommand(
                sessionId: String,
                command: RemotePlaybackCommand,
                seekPositionTicks: Long?,
            ): Result<Unit> = Result.success(Unit)
        }
        val viewModel = RemotePlaybackViewModel(repository)

        viewModel.refresh()
        runCurrent()
        assertEquals(1, fetchCount)

        viewModel.refresh() // A periodic poll during a slow request must not queue more network work.
        assertEquals(1, fetchCount)
        viewModel.sendCommand("session-1", RemotePlaybackCommand.PLAY_PAUSE)
        runCurrent()
        assertNotNull(viewModel.state.value.notice)
        assertEquals(1, fetchCount)

        firstFetch.complete(Result.success(listOf(session())))
        advanceUntilIdle()

        assertEquals(2, fetchCount)
        assertEquals("Atualizado", viewModel.state.value.sessions.single().itemName)
        assertFalse(viewModel.state.value.isLoading)
    }

    private fun session() = RemotePlaybackSession("session-1", "TV", "Android TV", "Filme", false, true, 0L)

    private class FakeRemotePlaybackRepository(
        private val sessions: List<RemotePlaybackSession> = emptyList(),
        private val commandResult: Result<Unit> = Result.success(Unit),
        private val commandGate: CompletableDeferred<Result<Unit>>? = null,
    ) : RemotePlaybackRepository {
        var sessionsResult: Result<List<RemotePlaybackSession>> = Result.success(sessions)
        val commands = mutableListOf<Pair<String, RemotePlaybackCommand>>()
        override suspend fun getActiveSessions() = sessionsResult
        override suspend fun sendCommand(
            sessionId: String,
            command: RemotePlaybackCommand,
            seekPositionTicks: Long?,
        ): Result<Unit> {
            commands += sessionId to command
            return commandGate?.await() ?: commandResult
        }
    }
}
