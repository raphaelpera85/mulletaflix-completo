package org.mulletaflix.feature.syncplay

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import kotlinx.coroutines.withContext
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackIdentity
import org.mulletaflix.domain.repository.RemotePlaybackRepository
import org.mulletaflix.domain.repository.RemotePlaybackSession
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class RemotePlaybackViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before fun setUp() = Dispatchers.setMain(dispatcher)
    @After fun tearDown() = Dispatchers.resetMain()

    @Test
    fun `refresh publishes only sessions provided by repository`() = runTest {
        val expected = listOf(session())
        val viewModel = viewModel(FakeRemotePlaybackRepository(sessions = expected))

        viewModel.refresh()
        advanceUntilIdle()

        assertEquals(expected, viewModel.state.value.sessions)
        assertFalse(viewModel.state.value.isLoading)
        assertEquals(null, viewModel.state.value.error)
    }

    @Test
    fun `failed refresh clears stale sessions and exposes retryable error`() = runTest {
        val repository = FakeRemotePlaybackRepository(sessions = listOf(session()))
        val viewModel = viewModel(repository)
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
        val viewModel = viewModel(repository)
        viewModel.refresh()
        advanceUntilIdle()

        viewModel.sendCommand(identity("user-a", "server-a"), "session-1", RemotePlaybackCommand.PLAY_PAUSE)
        advanceUntilIdle()

        assertEquals(1, viewModel.state.value.sessions.size)
        assertEquals("Dispositivo desconectado", viewModel.state.value.error)
        assertEquals(null, viewModel.state.value.busySessionId)
    }

    @Test
    fun `duplicate command is ignored while the first command is pending`() = runTest {
        val gate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRemotePlaybackRepository(sessions = listOf(session()), commandGate = gate)
        val viewModel = viewModel(repository)

        runCurrent()
        viewModel.refresh()
        advanceUntilIdle()
        viewModel.sendCommand(identity("user-a", "server-a"), "session-1", RemotePlaybackCommand.PLAY_PAUSE)
        runCurrent()
        viewModel.sendCommand(identity("user-a", "server-a"), "session-2", RemotePlaybackCommand.STOP)
        assertEquals("session-1", viewModel.state.value.busySessionId)
        assertEquals(listOf("session-1" to RemotePlaybackCommand.PLAY_PAUSE), repository.commands)

        gate.complete(Result.success(Unit))
        advanceUntilIdle()
        assertNotNull(viewModel.state.value.notice)
    }

    @Test
    fun `refresh requested during in-flight refresh runs after current response`() = runTest {
        val slowFetch = CompletableDeferred<Result<List<RemotePlaybackSession>>>()
        var fetchCount = 0
        val repository = object : RemotePlaybackRepository {
            override suspend fun getActiveSessions(identity: RemotePlaybackIdentity): Result<List<RemotePlaybackSession>> {
                fetchCount += 1
                return when (fetchCount) {
                    1 -> Result.success(listOf(session()))
                    2 -> slowFetch.await()
                    else -> Result.success(listOf(session().copy(itemName = "Atualizado")))
                }
            }

            override suspend fun sendCommand(
                identity: RemotePlaybackIdentity,
                sessionId: String,
                command: RemotePlaybackCommand,
                seekPositionTicks: Long?,
            ): Result<Unit> = Result.success(Unit)
        }
        val viewModel = viewModel(repository)

        viewModel.refresh()
        advanceUntilIdle()
        assertEquals(1, fetchCount)

        viewModel.refresh() // A periodic poll during a slow request must not queue more network work.
        runCurrent()
        assertEquals(2, fetchCount)
        viewModel.refresh()
        assertEquals(2, fetchCount)
        viewModel.sendCommand(identity("user-a", "server-a"), "session-1", RemotePlaybackCommand.PLAY_PAUSE)
        runCurrent()
        assertNotNull(viewModel.state.value.notice)
        assertEquals(2, fetchCount)

        slowFetch.complete(Result.success(listOf(session())))
        advanceUntilIdle()

        assertEquals(3, fetchCount)
        assertEquals("Atualizado", viewModel.state.value.sessions.single().itemName)
        assertFalse(viewModel.state.value.isLoading)
    }

    @Test
    fun `late response from previous account cannot replace current sessions`() = runTest {
        val oldResponse = CompletableDeferred<Result<List<RemotePlaybackSession>>>()
        val repository = object : RemotePlaybackRepository {
            val requestedUsers = mutableListOf<String>()
            override suspend fun getActiveSessions(identity: RemotePlaybackIdentity): Result<List<RemotePlaybackSession>> {
                requestedUsers += identity.userId
                return if (identity.userId == "user-a") {
                    withContext(NonCancellable) { oldResponse.await() }
                } else {
                    Result.success(listOf(session().copy(itemName = "Conta B")))
                }
            }

            override suspend fun sendCommand(
                identity: RemotePlaybackIdentity,
                sessionId: String,
                command: RemotePlaybackCommand,
                seekPositionTicks: Long?,
            ): Result<Unit> = Result.success(Unit)
        }
        val sessions = FakeSessionRepository(feedbackSession("user-a", "server-a"))
        val viewModel = RemotePlaybackViewModel(repository, sessions)

        runCurrent()
        viewModel.refresh()
        runCurrent()
        sessions.current.value = feedbackSession("user-b", "server-a")
        runCurrent()
        oldResponse.complete(Result.success(listOf(session().copy(itemName = "Resposta antiga A"))))
        advanceUntilIdle()

        assertEquals(listOf("user-a", "user-b"), repository.requestedUsers)
        assertEquals("Conta B", viewModel.state.value.sessions.single().itemName)
        assertFalse(viewModel.state.value.isLoading)
    }

    @Test
    fun `changing lan and public endpoint for same server preserves current state`() = runTest {
        val repository = FakeRemotePlaybackRepository(sessions = listOf(session()))
        val sessions = FakeSessionRepository(feedbackSession("user-a", "server-a"))
        val viewModel = RemotePlaybackViewModel(repository, sessions)

        runCurrent()
        viewModel.refresh()
        advanceUntilIdle()
        sessions.current.value = feedbackSession("user-a", "server-a", "http://192.168.1.20:8096")
        runCurrent()

        assertEquals(listOf(session()), viewModel.state.value.sessions)
        assertEquals(1, repository.fetchCount)
        assertEquals(null, viewModel.state.value.error)
    }

    @Test
    fun `late command result from previous account cannot publish success notice`() = runTest {
        val commandGate = CompletableDeferred<Result<Unit>>()
        val repository = FakeRemotePlaybackRepository(
            sessions = listOf(session()),
            commandGate = commandGate,
        )
        val sessions = FakeSessionRepository(feedbackSession("user-a", "server-a"))
        val viewModel = RemotePlaybackViewModel(repository, sessions)

        runCurrent()
        viewModel.refresh()
        advanceUntilIdle()
        viewModel.sendCommand(identity("user-a", "server-a"), "session-1", RemotePlaybackCommand.PLAY_PAUSE)
        runCurrent()
        sessions.current.value = feedbackSession("user-b", "server-b")
        runCurrent()
        commandGate.complete(Result.success(Unit))
        advanceUntilIdle()

        assertEquals(null, viewModel.state.value.notice)
        assertEquals(null, viewModel.state.value.busySessionId)
        assertEquals("user-b", repository.requestedIdentities.last().userId)
        val callCount = repository.commands.size
        viewModel.sendCommand(identity("user-a", "server-a"), "session-1", RemotePlaybackCommand.STOP)
        runCurrent()
        assertEquals(callCount, repository.commands.size)
    }

    private fun session() = RemotePlaybackSession("session-1", "TV", "Android TV", "Filme", false, true, 0L)

    private fun viewModel(repository: RemotePlaybackRepository) =
        RemotePlaybackViewModel(repository, FakeSessionRepository(feedbackSession("user-a", "server-a")))

    private class FakeRemotePlaybackRepository(
        private val sessions: List<RemotePlaybackSession> = emptyList(),
        private val commandResult: Result<Unit> = Result.success(Unit),
        private val commandGate: CompletableDeferred<Result<Unit>>? = null,
    ) : RemotePlaybackRepository {
        var sessionsResult: Result<List<RemotePlaybackSession>> = Result.success(sessions)
        var fetchCount = 0
        val requestedIdentities = mutableListOf<RemotePlaybackIdentity>()
        val commands = mutableListOf<Pair<String, RemotePlaybackCommand>>()
        override suspend fun getActiveSessions(identity: RemotePlaybackIdentity): Result<List<RemotePlaybackSession>> {
            fetchCount++
            requestedIdentities += identity
            return sessionsResult
        }
        override suspend fun sendCommand(
            identity: RemotePlaybackIdentity,
            sessionId: String,
            command: RemotePlaybackCommand,
            seekPositionTicks: Long?,
        ): Result<Unit> {
            requestedIdentities += identity
            commands += sessionId to command
            return commandGate?.let { withContext(NonCancellable) { it.await() } } ?: commandResult
        }
    }

    private fun feedbackSession(
        userId: String,
        serverId: String,
        serverUrl: String = "https://mulletaflix.example",
    ) = FeedbackRequestSession(serverUrl, "token-$userId", userId, "device-$userId", serverId)

    private fun identity(userId: String, serverId: String) =
        RemotePlaybackIdentity(serverId, "https://mulletaflix.example", userId)

    private class FakeSessionRepository(initialSession: FeedbackRequestSession?) : SessionRepository {
        val current = MutableStateFlow(initialSession)
        override fun getFeedbackRequestSession(): Flow<FeedbackRequestSession?> = current
        override fun getAccessToken() = kotlinx.coroutines.flow.flowOf(current.value?.accessToken)
        override fun getDeviceId() = kotlinx.coroutines.flow.flowOf(current.value?.deviceId.orEmpty())
        override fun getBaseUrl() = kotlinx.coroutines.flow.flowOf(current.value?.serverUrl.orEmpty())
        override fun getCurrentUserId() = kotlinx.coroutines.flow.flowOf(current.value?.userId)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() { current.value = null }
    }
}
