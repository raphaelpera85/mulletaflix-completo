package org.mulletaflix.feature.settings

import android.content.Context
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
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.*
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.VerifyServerUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class SettingsViewModelTest {

    private val dispatcher = StandardTestDispatcher()
    private val context = mockk<Context>(relaxed = true)

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun `initial state loads settings and user from repositories`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository(url = "http://localhost:8096", name = "Mulleta User")
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        val state = viewModel.state.value
        assertEquals("http://localhost:8096", state.serverUrl)
        assertEquals("Mulleta User", state.username)
        assertEquals(MulletaFlixThemeVariant.Dark, state.theme)
        assertTrue(state.autoPlay)
        assertTrue(state.skipIntro)
        assertTrue(state.pictureInPicture)
    }

    @Test
    fun `setTheme updates state and repository`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository()
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        viewModel.setTheme(MulletaFlixThemeVariant.Netflix)
        advanceUntilIdle()

        assertEquals(MulletaFlixThemeVariant.Netflix, viewModel.state.value.theme)
        assertEquals(AppThemeSetting.Netflix, settingsRepo.currentTheme)
    }

    @Test
    fun `toggle playback options updates state and repository`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository()
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        viewModel.setAutoPlay(false)
        viewModel.setSkipIntro(false)
        viewModel.setPictureInPicture(false)
        viewModel.setDefaultPlaybackSpeed(1.5f)
        advanceUntilIdle()

        assertFalse(viewModel.state.value.autoPlay)
        assertFalse(viewModel.state.value.skipIntro)
        assertFalse(viewModel.state.value.pictureInPicture)
        assertEquals(1.5f, viewModel.state.value.defaultSpeed, 0.01f)
    }

    @Test
    fun `logout calls logoutUseCase`() = runTest {
        var loggedOut = false
        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> {
                loggedOut = true
                return Result.success(Unit)
            }
        }
        val settingsRepo = FakeSettingsRepository()
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        viewModel.logout()
        advanceUntilIdle()

        assertTrue(loggedOut)
    }

    @Test
    fun `server connection check exposes latency and version`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository(
            url = "http://mulletaflix.duckdns.org:8096",
            verification = Result.success(ServerVerification("MulletaFlix", "12.0.11", 42L)),
        )
        val viewModel = SettingsViewModel(
            context,
            settingsRepo,
            authRepo,
            LogoutUseCase(authRepo),
            verifyServerUseCase = VerifyServerUseCase(authRepo),
        )
        advanceUntilIdle()

        viewModel.checkServerConnection()
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isCheckingConnection)
        assertEquals("Conectado • 42 ms • v12.0.11", viewModel.state.value.connectionStatus)
    }

    @Test
    fun `server connection check exposes a friendly failure`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository(
            verification = Result.failure(IllegalStateException("Servidor indisponível")),
        )
        val viewModel = SettingsViewModel(
            context,
            settingsRepo,
            authRepo,
            LogoutUseCase(authRepo),
            verifyServerUseCase = VerifyServerUseCase(authRepo),
        )
        advanceUntilIdle()

        viewModel.checkServerConnection()
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isCheckingConnection)
        assertEquals("Servidor indisponível", viewModel.state.value.connectionStatus)
    }

    private class FakeSettingsRepository : SettingsRepository {
        var currentTheme: AppThemeSetting = AppThemeSetting.Dark
        var autoPlay: Boolean = true
        var skipIntro: Boolean = true
        var pip: Boolean = true
        var quality: String = "Auto"
        var speed: Float = 1.0f
        var subtitleSize: Int = 100

        override fun getTheme(): Flow<AppThemeSetting> = MutableStateFlow(currentTheme)
        override suspend fun setTheme(theme: AppThemeSetting) { currentTheme = theme }

        override fun getMaxBitrate(): Flow<Int> = flowOf(0)
        override suspend fun setMaxBitrate(bitrate: Int) = Unit

        override fun isPiPEnabled(): Flow<Boolean> = MutableStateFlow(pip)
        override suspend fun setPiPEnabled(enabled: Boolean) { pip = enabled }

        override fun getPreferredAudioLanguage(): Flow<String?> = flowOf(null)
        override suspend fun setPreferredAudioLanguage(language: String?) = Unit

        override fun getPreferredSubtitleLanguage(): Flow<String?> = flowOf("pt-br")
        override suspend fun setPreferredSubtitleLanguage(language: String?) = Unit

        override fun isAutoPlayEnabled(): Flow<Boolean> = MutableStateFlow(autoPlay)
        override suspend fun setAutoPlayEnabled(enabled: Boolean) { autoPlay = enabled }

        override fun isSkipIntroEnabled(): Flow<Boolean> = MutableStateFlow(skipIntro)
        override suspend fun setSkipIntroEnabled(enabled: Boolean) { skipIntro = enabled }

        override fun getDefaultQuality(): Flow<String> = MutableStateFlow(quality)
        override suspend fun setDefaultQuality(quality: String) { this.quality = quality }

        override fun getDefaultPlaybackSpeed(): Flow<Float> = MutableStateFlow(speed)
        override suspend fun setDefaultPlaybackSpeed(speed: Float) { this.speed = speed }

        override suspend fun clearLocalPreferences() = Unit

        override fun getSubtitleFontSize(): Flow<Int> = MutableStateFlow(subtitleSize)
        override suspend fun setSubtitleFontSize(size: Int) { subtitleSize = size }
        override fun getDefaultAspectRatio(): Flow<String> = MutableStateFlow("FIT")
        override suspend fun setDefaultAspectRatio(aspectRatio: String) = Unit
    }

    private open class FakeAuthRepository(
        private val url: String = "http://localhost:8096",
        private val name: String = "User",
        private val verification: Result<ServerVerification> = Result.failure(NotImplementedError()),
    ) : AuthRepository {
        override suspend fun verifyServer(url: String): Result<ServerVerification> = verification
        override suspend fun register(username: String, password: String): Result<RegistrationResult> = Result.failure(NotImplementedError())
        override suspend fun login(username: String, password: String): Result<UserSession> = Result.failure(NotImplementedError())
        override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(emptyList())
        override suspend fun initiateQuickConnect(): Result<QuickConnectState> = Result.failure(NotImplementedError())
        override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = Result.success(null)
        override suspend fun logout(): Result<Unit> = Result.success(Unit)
        override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.failure(NotImplementedError())
        override fun getSavedServerUrl(): Flow<String> = flowOf(url)
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId(): Flow<String?> = flowOf("u1")
        override fun getSavedUserName(): Flow<String?> = flowOf(name)
        override fun getSavedToken(): Flow<String?> = flowOf("token")
    }
}
