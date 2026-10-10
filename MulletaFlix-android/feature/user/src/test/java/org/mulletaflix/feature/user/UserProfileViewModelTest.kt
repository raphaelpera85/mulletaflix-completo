package org.mulletaflix.feature.user

import android.content.Context
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.*
import org.mulletaflix.domain.usecase.GetUserProfileUseCase
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.SwitchUserUseCase
import java.io.File

@OptIn(ExperimentalCoroutinesApi::class)
class UserProfileViewModelTest {

    private val dispatcher = StandardTestDispatcher()
    private val context = mockk<Context>(relaxed = true)

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
        val tempDir = File.createTempFile("mulleta_test_cache", "dir")
        tempDir.delete()
        tempDir.mkdirs()
        every { context.cacheDir } returns tempDir
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun createViewModel(
        authRepo: AuthRepository,
        sessionRepo: SessionRepository = FakeSessionRepository(),
        artworkCacheCleaner: org.mulletaflix.core.common.cache.ArtworkCacheCleaner =
            org.mulletaflix.core.common.cache.ArtworkCacheCleaner {},
    ): UserProfileViewModel {
        return UserProfileViewModel(
            authRepository = authRepo,
            sessionRepository = sessionRepo,
            getUserProfileUseCase = GetUserProfileUseCase(authRepo),
            logoutUseCase = LogoutUseCase(authRepo),
            switchUserUseCase = SwitchUserUseCase(authRepo),
            context = context,
            ioDispatcher = dispatcher,
            artworkCacheCleaner = artworkCacheCleaner,
        )
    }

    @Test
    fun `loadProfile successfully populates profile, server verification and available users`() = runTest {
        val expectedProfile = UserProfile(
            id = "u1",
            name = "Admin User",
            isAdministrator = true,
            canDownload = true,
            canAccessLiveTv = true,
            canPlayMedia = true,
        )
        val verification = ServerVerification(name = "Mulleta Server", version = "10.9.0", latencyMs = 15L)
        val otherUser = AvailableUser(id = "u2", name = "Guest User")

        val authRepo = object : FakeAuthRepository() {
            override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.success(expectedProfile)
            override suspend fun verifyServer(url: String): Result<ServerVerification> = Result.success(verification)
            override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(listOf(
                AvailableUser(id = "u1", name = "Admin User"),
                otherUser,
            ))
        }

        val sessionRepo = FakeSessionRepository(url = "http://192.168.1.100:8096", userId = "u1", userName = "Admin User")

        val viewModel = createViewModel(authRepo, sessionRepo)
        advanceUntilIdle()

        val state = viewModel.uiState.value
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertEquals("Admin User", state.userProfile?.name)
        assertTrue(state.userProfile?.isAdministrator == true)
        assertEquals("Mulleta Server", state.serverVerification?.name)
        assertEquals(1, state.availableUsers.size)
        assertEquals("Guest User", state.availableUsers.first().name)
    }

    @Test
    fun `loadProfile falls back to local session name when remote call fails`() = runTest {
        val authRepo = object : FakeAuthRepository() {
            override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.failure(Exception("Offline"))
        }
        val sessionRepo = FakeSessionRepository(url = "http://192.168.1.100:8096", userId = "u-fallback", userName = "Offline User")

        val viewModel = createViewModel(authRepo, sessionRepo)
        advanceUntilIdle()

        val state = viewModel.uiState.value
        assertEquals("Offline User", state.userProfile?.name)
        assertEquals("u-fallback", state.userProfile?.id)
        assertFalse(state.userProfile?.isAdministrator ?: true)
    }

    @Test
    fun `late profile from a previous user cannot replace the current session`() = runTest {
        val staleProfile = CompletableDeferred<Result<UserProfile>>()
        val userId = MutableStateFlow<String?>("u1")
        val currentProfile = UserProfile(
            id = "u2",
            name = "Current User",
            isAdministrator = false,
            canDownload = true,
            canAccessLiveTv = true,
            canPlayMedia = true,
        )
        val authRepo = object : FakeAuthRepository() {
            override suspend fun getCurrentUserProfile(): Result<UserProfile> =
                if (userId.value == "u1") staleProfile.await() else Result.success(currentProfile)
        }
        val sessionRepo = FakeSessionRepository(userIdFlow = userId, userNameFlow = MutableStateFlow("User 1"))
        val viewModel = createViewModel(authRepo, sessionRepo)
        advanceUntilIdle()

        userId.value = "u2"
        sessionRepo.userNameFlow.value = "Current User"
        advanceUntilIdle()

        assertEquals("Current User", viewModel.uiState.value.userProfile?.name)

        staleProfile.complete(
            Result.success(
                UserProfile(
                    id = "u1",
                    name = "Old User",
                    isAdministrator = true,
                    canDownload = true,
                    canAccessLiveTv = true,
                    canPlayMedia = true,
                ),
            ),
        )
        advanceUntilIdle()

        assertEquals("Current User", viewModel.uiState.value.userProfile?.name)
    }

    @Test
    fun `selectUserToSwitch and confirmSwitchUser executes switch with login`() = runTest {
        val target = AvailableUser(id = "u2", name = "Guest")
        var loggedInUser: String? = null
        var loginSuccessCallbackCalled = false

        val authRepo = object : FakeAuthRepository() {
            override suspend fun login(username: String, password: String): Result<UserSession> {
                loggedInUser = username
                return Result.success(UserSession("u2", username, "token-2", "srv-1"))
            }
        }
        val sessionRepo = FakeSessionRepository(url = "http://192.168.1.100:8096", userId = "u1", userName = "Admin")

        val viewModel = createViewModel(authRepo, sessionRepo)
        advanceUntilIdle()

        viewModel.selectUserToSwitch(target)
        assertTrue(viewModel.uiState.value.isSwitchDialogOpen)
        assertEquals(target, viewModel.uiState.value.selectedUserForSwitch)

        viewModel.onSwitchPasswordChanged("1234")
        assertEquals("1234", viewModel.uiState.value.switchPasswordInput)

        viewModel.confirmSwitchUser {
            loginSuccessCallbackCalled = true
        }
        advanceUntilIdle()

        assertEquals("Guest", loggedInUser)
        assertTrue(loginSuccessCallbackCalled)
        assertFalse(viewModel.uiState.value.isSwitchDialogOpen)
        assertNull(viewModel.uiState.value.selectedUserForSwitch)
    }

    @Test
    fun `repeated switch confirmation while login is pending submits only once`() = runTest {
        val target = AvailableUser(id = "u2", name = "Guest")
        val loginResult = CompletableDeferred<Result<UserSession>>()
        var loginCalls = 0
        var successCallbacks = 0
        val authRepo = object : FakeAuthRepository() {
            override suspend fun login(username: String, password: String): Result<UserSession> {
                loginCalls++
                return loginResult.await()
            }
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()
        viewModel.selectUserToSwitch(target)
        viewModel.onSwitchPasswordChanged("1234")

        viewModel.confirmSwitchUser { successCallbacks++ }
        viewModel.confirmSwitchUser { successCallbacks++ }
        runCurrent()

        assertEquals(1, loginCalls)
        assertTrue(viewModel.uiState.value.isSwitchingUser)

        loginResult.complete(Result.success(UserSession("u2", "Guest", "token-2", "srv-1")))
        advanceUntilIdle()

        assertEquals(1, successCallbacks)
        assertFalse(viewModel.uiState.value.isSwitchingUser)
        assertFalse(viewModel.uiState.value.isSwitchDialogOpen)
    }

    @Test
    fun `logout triggers authRepository logout and executes onComplete callback`() = runTest {
        var authLogoutCalled = false
        var onCompleteCalled = false

        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> {
                authLogoutCalled = true
                return Result.success(Unit)
            }
        }
        val sessionRepo = FakeSessionRepository()

        val viewModel = createViewModel(authRepo, sessionRepo)
        advanceUntilIdle()

        viewModel.logout {
            onCompleteCalled = true
        }
        advanceUntilIdle()

        assertTrue(authLogoutCalled)
        assertTrue(onCompleteCalled)
    }

    @Test
    fun `failed logout keeps the current destination and exposes the failure`() = runTest {
        var onCompleteCalled = false
        val failure = IllegalStateException("session storage unavailable")
        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> = Result.failure(failure)
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.logout { onCompleteCalled = true }
        advanceUntilIdle()

        assertFalse("Navigation must wait until persisted credentials are cleared", onCompleteCalled)
        assertFalse("The progress state must settle after a failed logout", viewModel.uiState.value.isLoggingOut)
        assertEquals(failure.message, viewModel.uiState.value.error)
    }

    @Test
    fun `retrying logout clears stale error before request starts`() = runTest {
        val retryResult = CompletableDeferred<Result<Unit>>()
        var logoutCalls = 0
        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> {
                logoutCalls++
                return if (logoutCalls == 1) {
                    Result.failure(IllegalStateException("first attempt failed"))
                } else {
                    retryResult.await()
                }
            }
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.logout {}
        advanceUntilIdle()
        assertEquals("first attempt failed", viewModel.uiState.value.error)

        viewModel.logout {}
        assertNull("A new attempt must not display the previous failure", viewModel.uiState.value.error)
        assertTrue(viewModel.uiState.value.isLoggingOut)

        runCurrent()
        retryResult.complete(Result.success(Unit))
        advanceUntilIdle()
        assertNull(viewModel.uiState.value.error)
        assertFalse(viewModel.uiState.value.isLoggingOut)
    }

    @Test
    fun `duplicate logout requests while first request is pending are ignored`() = runTest {
        val logoutResult = CompletableDeferred<Result<Unit>>()
        var logoutCalls = 0
        var completedCallbacks = 0
        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> {
                logoutCalls++
                return logoutResult.await()
            }
        }
        val viewModel = createViewModel(authRepo)
        advanceUntilIdle()

        viewModel.logout { completedCallbacks++ }
        viewModel.logout { completedCallbacks++ }
        runCurrent()

        assertEquals("Only one session-clear request may run at a time", 1, logoutCalls)
        assertTrue(viewModel.uiState.value.isLoggingOut)

        logoutResult.complete(Result.success(Unit))
        advanceUntilIdle()
        assertEquals(1, completedCallbacks)
        assertFalse(viewModel.uiState.value.isLoggingOut)
    }

    @Test
    fun `clearCache deletes cache files and sets cacheCleared flag`() = runTest {
        val authRepo = FakeAuthRepository()
        val sessionRepo = FakeSessionRepository()
        var artworkCacheCleared = false
        val imageCache = requireNotNull(context.cacheDir).resolve("image_cache").apply {
            mkdirs()
            resolve("poster.bin").writeBytes(byteArrayOf(1, 2, 3))
        }

        val viewModel = createViewModel(
            authRepo,
            sessionRepo,
            artworkCacheCleaner = org.mulletaflix.core.common.cache.ArtworkCacheCleaner {
                artworkCacheCleared = true
                imageCache.deleteRecursively()
            },
        )
        advanceUntilIdle()

        viewModel.clearCache()
        advanceUntilIdle()

        assertTrue(viewModel.uiState.value.cacheCleared)
        assertTrue(viewModel.uiState.value.message?.contains("sucesso") == true)
        assertTrue("profile cleanup clears Coil memory and disk caches", artworkCacheCleared)
        assertFalse(imageCache.resolve("poster.bin").exists())
    }

    // ── Fakes ────────────────────────────────────────────────────────────────
    private open class FakeAuthRepository : AuthRepository {
        override suspend fun verifyServer(url: String): Result<ServerVerification> = Result.success(ServerVerification("Test Server", "1.0"))
        override suspend fun register(username: String, password: String): Result<RegistrationResult> = Result.failure(NotImplementedError())
        override suspend fun login(username: String, password: String): Result<UserSession> = Result.failure(NotImplementedError())
        override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(emptyList())
        override suspend fun initiateQuickConnect(): Result<QuickConnectState> = Result.failure(NotImplementedError())
        override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = Result.success(null)
        override suspend fun logout(): Result<Unit> = Result.success(Unit)
        override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.failure(NotImplementedError())
        override fun getSavedServerUrl(): Flow<String> = flowOf("http://localhost:8096")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId(): Flow<String?> = flowOf("u1")
        override fun getSavedUserName(): Flow<String?> = flowOf("Admin User")
        override fun getSavedToken(): Flow<String?> = flowOf("token")
    }

    private open class FakeSessionRepository(
        private val url: String = "http://localhost:8096",
        private val userId: String? = "u1",
        private val userName: String? = "Admin User",
        val userIdFlow: MutableStateFlow<String?> = MutableStateFlow(userId),
        val userNameFlow: MutableStateFlow<String?> = MutableStateFlow(userName),
    ) : SessionRepository {
        override fun getAccessToken(): Flow<String?> = flowOf("token")
        override fun getDeviceId(): Flow<String> = flowOf("dev-1")
        override fun getBaseUrl(): Flow<String> = flowOf(url)
        override fun getCurrentUserId(): Flow<String?> = userIdFlow
        override fun getCurrentUserName(): Flow<String?> = userNameFlow
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
