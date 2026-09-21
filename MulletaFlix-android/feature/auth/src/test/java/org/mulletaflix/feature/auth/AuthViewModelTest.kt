package org.mulletaflix.feature.auth

import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.delay
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import kotlinx.coroutines.withContext
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.*
import org.mulletaflix.domain.usecase.LoginUseCase
import org.mulletaflix.domain.usecase.RegisterUseCase
import org.mulletaflix.domain.usecase.VerifyServerUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class AuthViewModelTest {

    private val dispatcher = StandardTestDispatcher()
    private val discovery = mockk<LocalServerDiscovery>(relaxed = true)

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
        coEvery { discovery.discover(any()) } returns listOf(ServerInfo(name = "LAN Server", url = "http://192.168.1.10:8096"))
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun createViewModel(
        authRepo: AuthRepository = FakeAuthRepository(),
    ): AuthViewModel {
        return AuthViewModel(
            authRepository = authRepo,
            localServerDiscovery = discovery,
            loginUseCase = LoginUseCase(authRepo),
            registerUseCase = RegisterUseCase(authRepo),
            verifyServerUseCase = VerifyServerUseCase(authRepo),
        )
    }

    @Test
    fun `initial state reflects saved server and empty credentials`() = runTest {
        val viewModel = createViewModel()
        advanceUntilIdle()

        val state = viewModel.state.value
        assertEquals("", state.username)
        assertEquals("", state.password)
        assertFalse(state.isLoading)
        assertFalse(state.isAuthenticated)
    }

    @Test
    fun `login with valid credentials sets isAuthenticated to true`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun login(username: String, password: String): Result<UserSession> {
                return Result.success(UserSession(userId = "u1", userName = username, token = "tok-1", serverId = "srv-1"))
            }
        }
        val viewModel = createViewModel(authRepo = authRepo)
        advanceUntilIdle()

        viewModel.onUsernameChange("raphael")
        viewModel.onPasswordChange("password123")
        viewModel.login()
        advanceUntilIdle()

        val state = viewModel.state.value
        assertTrue(state.isAuthenticated)
        assertNull(state.error)
    }

    @Test
    fun `login with blank username sets error`() = runTest {
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.login("", "password123")
        advanceUntilIdle()

        val state = viewModel.state.value
        assertFalse(state.isAuthenticated)
        assertEquals("Digite o nome de usuário", state.error)
    }

    @Test
    fun `quick connect availability is exposed and disabled servers get a clear message`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun isQuickConnectEnabled(): Result<Boolean> = Result.success(false)
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        assertEquals(false, viewModel.state.value.isQuickConnectAvailable)
        viewModel.initiateQuickConnect()
        advanceUntilIdle()

        assertEquals(
            "Quick Connect está desativado neste servidor. Use usuário e senha.",
            viewModel.state.value.error,
        )
    }

    @Test
    fun `already authorized quick connect authenticates without polling delay`() = runTest {
        var checkCalls = 0
        val authRepo = object : FakeAuthRepository() {
            override suspend fun initiateQuickConnect(): Result<QuickConnectState> =
                Result.success(QuickConnectState("123456", "authorized-secret", true))

            override suspend fun checkQuickConnect(secret: String): Result<UserSession?> {
                checkCalls++
                return Result.success(UserSession("u1", "Raphael", "token", "server-1"))
            }
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.initiateQuickConnect()
        advanceUntilIdle()

        assertEquals(1, checkCalls)
        assertTrue(viewModel.state.value.isAuthenticated)
        assertFalse(viewModel.state.value.isWaitingForQuickConnect)
        assertNull(viewModel.state.value.quickConnectSecret)
    }

    @Test
    fun `changing server cancels quick connect polling and clears its state`() = runTest {
        var pollCalls = 0
        val authRepo = object : FakeAuthRepository() {
            override suspend fun checkQuickConnect(secret: String): Result<UserSession?> {
                pollCalls++
                return Result.success(null)
            }
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.initiateQuickConnect()
        runCurrent()
        assertTrue(viewModel.state.value.isWaitingForQuickConnect)

        viewModel.connectToServer("http://192.168.1.99:8096", onSuccess = {})
        advanceUntilIdle()

        assertEquals(0, pollCalls)
        assertFalse(viewModel.state.value.isWaitingForQuickConnect)
        assertNull(viewModel.state.value.quickConnectSecret)
        assertNull(viewModel.state.value.quickConnectSecondsRemaining)
    }

    @Test
    fun `changing server invalidates quick connect initiation still in flight`() = runTest {
        var pollCalls = 0
        val authRepo = object : FakeAuthRepository() {
            override suspend fun initiateQuickConnect(): Result<QuickConnectState> {
                withContext(NonCancellable) { delay(100) }
                return Result.success(QuickConnectState("654321", "stale-secret", false))
            }

            override suspend fun checkQuickConnect(secret: String): Result<UserSession?> {
                pollCalls++
                return Result.success(null)
            }
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.initiateQuickConnect()
        runCurrent()
        viewModel.connectToServer("http://192.168.1.88:8096", onSuccess = {})
        advanceUntilIdle()

        assertEquals(0, pollCalls)
        assertFalse(viewModel.state.value.isWaitingForQuickConnect)
        assertNull(viewModel.state.value.quickConnectSecret)
    }

    @Test
    fun `late quick connect availability from previous server is ignored`() = runTest {
        coEvery { discovery.discover(any()) } returns emptyList()
        val oldAvailability = CompletableDeferred<Result<Boolean>>()
        val newAvailability = CompletableDeferred<Result<Boolean>>()
        var availabilityCalls = 0
        val authRepo = object : FakeAuthRepository() {
            override suspend fun isQuickConnectEnabled(): Result<Boolean> = when (++availabilityCalls) {
                1 -> oldAvailability.await()
                else -> newAvailability.await()
            }
        }
        val viewModel = createViewModel(authRepo)
        runCurrent()

        viewModel.connectToServer("http://new-server:8096", onSuccess = {})
        runCurrent()
        newAvailability.complete(Result.success(true))
        runCurrent()
        oldAvailability.complete(Result.success(false))
        advanceUntilIdle()

        assertEquals(true, viewModel.state.value.isQuickConnectAvailable)
        assertEquals("http://new-server:8096", viewModel.state.value.serverUrl)
    }

    @Test
    fun `connectToServer verifies and saves server`() = runTest {
        var serverSet: String? = null
        var savedServer: SavedServer? = null
        val authRepo = object : FakeAuthRepository() {
            override suspend fun verifyServer(url: String): Result<ServerVerification> {
                return Result.success(ServerVerification(name = "Mulleta Primary", version = "10.9.0", latencyMs = 12L, serverId = "srv-1"))
            }
            override suspend fun setServerUrl(url: String) {
                serverSet = url
            }
            override suspend fun addSavedServer(server: SavedServer) {
                savedServer = server
            }
        }
        val viewModel = createViewModel(authRepo = authRepo)
        advanceUntilIdle()

        var successCalled = false
        viewModel.connectToServer("http://192.168.1.50:8096", onSuccess = { successCalled = true })
        advanceUntilIdle()

        assertTrue(successCalled)
        assertEquals("http://192.168.1.50:8096", serverSet)
        assertEquals("http://192.168.1.50:8096", viewModel.state.value.serverUrl)
        assertEquals("srv-1", savedServer?.serverId)
    }

    @Test
    fun `register with valid info succeeds and triggers callback`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun register(username: String, password: String): Result<RegistrationResult> {
                return Result.success(RegistrationResult(success = true))
            }
        }
        val viewModel = createViewModel(authRepo = authRepo)
        advanceUntilIdle()

        var successCalled = false
        viewModel.register("newuser", "securepass123", onSuccess = { successCalled = true })
        advanceUntilIdle()

        assertTrue(successCalled)
        assertFalse(viewModel.state.value.isRegistering)
        assertNull(viewModel.state.value.error)
    }

    @Test
    fun `saved servers list always includes official duckdns server`() = runTest {
        val viewModel = createViewModel()
        advanceUntilIdle()

        val state = viewModel.state.value
        assertTrue(state.savedServers.any { it.url == DEFAULT_MULLETAFLIX_SERVER_URL })
    }

    @Test
    fun `connecting to multiple servers retains all of them in saved servers list`() = runTest {
        val authRepo = FakeAuthRepository()
        val viewModel = createViewModel(authRepo = authRepo)
        advanceUntilIdle()

        viewModel.connectToServer("http://192.168.1.100:8096", onSuccess = {})
        advanceUntilIdle()

        viewModel.connectToServer("http://192.168.1.200:8096", onSuccess = {})
        advanceUntilIdle()

        val savedUrls = viewModel.state.value.savedServers.map { it.url }
        assertTrue(savedUrls.contains("http://192.168.1.100:8096"))
        assertTrue(savedUrls.contains("http://192.168.1.200:8096"))
        assertTrue(savedUrls.contains(DEFAULT_MULLETAFLIX_SERVER_URL))
    }

    @Test
    fun `removing custom server removes it but official server cannot be removed`() = runTest {
        val authRepo = FakeAuthRepository()
        val viewModel = createViewModel(authRepo = authRepo)
        advanceUntilIdle()

        viewModel.connectToServer("http://192.168.1.100:8096", onSuccess = {})
        advanceUntilIdle()

        // Remove custom server
        viewModel.removeServer("http://192.168.1.100:8096")
        advanceUntilIdle()

        assertFalse(viewModel.state.value.savedServers.any { it.url == "http://192.168.1.100:8096" })

        // Attempt to remove official server - must NOT be removed
        viewModel.removeServer(DEFAULT_MULLETAFLIX_SERVER_URL)
        advanceUntilIdle()

        assertTrue(viewModel.state.value.savedServers.any { it.url == DEFAULT_MULLETAFLIX_SERVER_URL })
    }

    @Test
    fun `new LAN discovery cancels a stale slower discovery`() = runTest {
        var call = 0
        coEvery { discovery.discover(any()) } coAnswers {
            val currentCall = ++call
            if (currentCall == 2) delay(100)
            listOf(
                ServerInfo(
                    name = if (currentCall == 2) "Stale LAN" else "Fresh LAN",
                    url = "http://192.168.1.${if (currentCall == 2) 20 else 30}:8096",
                )
            )
        }
        val viewModel = createViewModel()
        advanceUntilIdle()
        call = 1 // The initial scan already consumed call 1.

        viewModel.discoverLocalServers()
        runCurrent()
        viewModel.discoverLocalServers()
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isDiscovering)
        assertEquals("Fresh LAN", viewModel.state.value.discoveredServers.single().name)
    }

    @Test
    fun `startup discovery prefers the saved server identity on the LAN`() = runTest {
        coEvery { discovery.discover(any()) } returns listOf(
            ServerInfo("Other LAN", "http://192.168.1.20:8096", serverId = "other-id"),
            ServerInfo("Saved LAN", "http://192.168.1.10:8096", serverId = "saved-id"),
        )
        val authRepo = object : FakeAuthRepository() {
            init {
                savedServersState.value = listOf(
                    SavedServer(
                        name = "MulletaFlix Cloud",
                        url = DEFAULT_MULLETAFLIX_SERVER_URL,
                        version = "12.0.2",
                        serverId = "saved-id",
                    )
                )
            }
        }

        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        assertEquals("http://192.168.1.10:8096", viewModel.state.value.serverUrl)
    }

    @Test
    fun `connecting to a new endpoint refreshes the available user picker`() = runTest {
        var availableUsersCalls = 0
        val authRepo = object : FakeAuthRepository() {
            override fun getSavedServerUrl(): Flow<String> = flowOf("")

            override suspend fun getAvailableUsers(): Result<List<AvailableUser>> {
                availableUsersCalls += 1
                return Result.success(listOf(AvailableUser(id = "u1", name = "Raphael")))
            }
        }
        coEvery { discovery.discover(any()) } returns emptyList()
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.connectToServer("http://192.168.1.10:8096", onSuccess = {})
        advanceUntilIdle()

        assertEquals(1, availableUsersCalls)
        assertEquals(listOf("Raphael"), viewModel.state.value.availableUsers.map { it.name })
    }

    @Test
    fun `failed user picker load can be retried for the same endpoint`() = runTest {
        var availableUsersCalls = 0
        val authRepo = object : FakeAuthRepository() {
            override fun getSavedServerUrl(): Flow<String> = flowOf("")

            override suspend fun getAvailableUsers(): Result<List<AvailableUser>> {
                availableUsersCalls += 1
                return if (availableUsersCalls == 1) {
                    Result.failure(IllegalStateException("temporary network failure"))
                } else {
                    Result.success(listOf(AvailableUser(id = "u2", name = "Retry User")))
                }
            }
        }
        coEvery { discovery.discover(any()) } returns emptyList()
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.connectToServer("http://192.168.1.11:8096", onSuccess = {})
        advanceUntilIdle()
        assertTrue(viewModel.state.value.availableUsers.isEmpty())

        viewModel.connectToServer("http://192.168.1.11:8096", onSuccess = {})
        advanceUntilIdle()

        assertEquals(2, availableUsersCalls)
        assertEquals(listOf("Retry User"), viewModel.state.value.availableUsers.map { it.name })
    }

    @Test
    fun `latest server connection wins over a late verification response`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun verifyServer(url: String): Result<ServerVerification> {
                if (url.contains("old")) {
                    withContext(NonCancellable) { delay(100) }
                }
                return Result.success(
                    ServerVerification(
                        name = "Verified $url",
                        version = "12.0.2",
                        latencyMs = 10L,
                        serverId = url,
                    )
                )
            }
        }
        coEvery { discovery.discover(any()) } returns emptyList()
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.connectToServer("http://old-server:8096", onSuccess = {})
        runCurrent()
        viewModel.connectToServer("http://new-server:8096", onSuccess = {})
        advanceUntilIdle()

        assertEquals("http://new-server:8096", viewModel.state.value.serverUrl)
        assertFalse(viewModel.state.value.savedServers.any { it.url == "http://old-server:8096" })
    }

    private open class FakeAuthRepository : AuthRepository {
        val savedServersState = MutableStateFlow<List<SavedServer>>(
            listOf(
                SavedServer(
                    name = "MulletaFlix Oficial (Nuvem)",
                    url = DEFAULT_MULLETAFLIX_SERVER_URL,
                    version = "12.0.2",
                )
            )
        )

        override suspend fun verifyServer(url: String): Result<ServerVerification> = Result.success(ServerVerification("Test $url", "12.0.2", 15L))
        override suspend fun register(username: String, password: String): Result<RegistrationResult> = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String): Result<UserSession> = Result.success(UserSession("u1", username, "token", "s1"))
        override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(emptyList())
        override suspend fun isQuickConnectEnabled(): Result<Boolean> = Result.success(true)
        override suspend fun initiateQuickConnect(): Result<QuickConnectState> = Result.success(QuickConnectState("123456", "secret", false))
        override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = Result.success(null)
        override suspend fun logout(): Result<Unit> = Result.success(Unit)
        override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.failure(NotImplementedError())
        override fun getSavedServerUrl(): Flow<String> = flowOf(DEFAULT_MULLETAFLIX_SERVER_URL)
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId(): Flow<String?> = flowOf("u1")
        override fun getSavedUserName(): Flow<String?> = flowOf("User")
        override fun getSavedToken(): Flow<String?> = flowOf(null)
        override fun getSavedServers(): Flow<List<SavedServer>> = savedServersState
        override suspend fun addSavedServer(server: SavedServer) {
            val list = savedServersState.value.toMutableList()
            list.removeAll { it.url == server.url }
            list.add(0, server)
            savedServersState.value = list
        }
        override suspend fun removeSavedServer(url: String) {
            if (url == DEFAULT_MULLETAFLIX_SERVER_URL) return
            savedServersState.value = savedServersState.value.filterNot { it.url == url }
        }
    }
}
