package org.mulletaflix.feature.auth

import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
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
    fun `connectToServer verifies and saves server`() = runTest {
        var serverSet: String? = null
        val authRepo = object : FakeAuthRepository() {
            override suspend fun verifyServer(url: String): Result<ServerVerification> {
                return Result.success(ServerVerification(name = "Mulleta Primary", version = "10.9.0", latencyMs = 12L))
            }
            override suspend fun setServerUrl(url: String) {
                serverSet = url
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

    private open class FakeAuthRepository : AuthRepository {
        override suspend fun verifyServer(url: String): Result<ServerVerification> = Result.success(ServerVerification("Test", "1"))
        override suspend fun register(username: String, password: String): Result<RegistrationResult> = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String): Result<UserSession> = Result.success(UserSession("u1", username, "token", "s1"))
        override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(emptyList())
        override suspend fun initiateQuickConnect(): Result<QuickConnectState> = Result.success(QuickConnectState("123456", "secret", false))
        override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = Result.success(null)
        override suspend fun logout(): Result<Unit> = Result.success(Unit)
        override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.failure(NotImplementedError())
        override fun getSavedServerUrl(): Flow<String> = flowOf("http://localhost:8096")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId(): Flow<String?> = flowOf("u1")
        override fun getSavedUserName(): Flow<String?> = flowOf("User")
        override fun getSavedToken(): Flow<String?> = flowOf(null)
    }
}
