package org.mulletaflix.feature.user

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.GetUserProfileUseCase
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.SwitchUserUseCase
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicInteger

/** Exercises the ProfileScreen confirmation dialogs through their visible actions. */
@RunWith(AndroidJUnit4::class)
class ProfileSessionConfirmationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun cancelLogoutDismissesConfirmationWithoutCallingRepositoryOrNavigation() {
        val auth = RecordingProfileAuthRepository()
        val viewModel = createViewModel(auth)
        val navigations = AtomicInteger()
        composeRule.setContent {
            MaterialTheme { ProfileScreen(viewModel = viewModel, onLogout = { navigations.incrementAndGet() }) }
        }

        composeRule.onNodeWithText("Sair da Conta").performScrollTo().performClick()
        composeRule.onNodeWithText("Encerrar Sessão").assertIsDisplayed()
        composeRule.onNodeWithText("Cancelar").performClick()

        composeRule.onNodeWithText("Encerrar Sessão").assertDoesNotExist()
        composeRule.runOnIdle {
            assertEquals(0, auth.logoutCalls)
            assertEquals(0, navigations.get())
        }
    }

    @Test
    fun confirmLogoutCallsRepositoryAndNavigatesOnlyAfterSuccess() {
        val auth = RecordingProfileAuthRepository()
        val viewModel = createViewModel(auth)
        val navigations = AtomicInteger()
        composeRule.setContent {
            MaterialTheme { ProfileScreen(viewModel = viewModel, onLogout = { navigations.incrementAndGet() }) }
        }

        composeRule.onNodeWithText("Sair da Conta").performScrollTo().performClick()
        composeRule.onNodeWithText("Sair").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { navigations.get() == 1 }

        composeRule.runOnIdle {
            assertEquals(1, auth.logoutCalls)
            assertEquals(1, navigations.get())
        }
    }

    @Test
    fun cancelAccountSwitchDismissesConfirmationWithoutLoggingIn() {
        val auth = RecordingProfileAuthRepository()
        val viewModel = createViewModel(auth)
        composeRule.setContent { MaterialTheme { ProfileScreen(viewModel = viewModel) } }

        composeRule.onNodeWithText("Visitante").performScrollTo().performClick()
        composeRule.onNodeWithText("Entrar como Visitante").assertIsDisplayed()
        composeRule.onNodeWithText("Cancelar").performClick()

        composeRule.onNodeWithText("Entrar como Visitante").assertDoesNotExist()
        composeRule.runOnIdle { assertEquals(emptyList<Pair<String, String>>(), auth.loginAttempts) }
    }

    @Test
    fun confirmAccountSwitchAuthenticatesSelectedUserAndClosesDialog() {
        val auth = RecordingProfileAuthRepository()
        val viewModel = createViewModel(auth)
        composeRule.setContent { MaterialTheme { ProfileScreen(viewModel = viewModel) } }

        composeRule.onNodeWithText("Visitante").performScrollTo().performClick()
        composeRule.onNodeWithText("Senha (opcional se livre)")
            .performClick()
            .performTextInput("senha-de-teste")
        composeRule.onNodeWithText("Alternar").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { auth.loginAttempts.isNotEmpty() }

        composeRule.runOnIdle {
            assertEquals(listOf("Visitante" to "senha-de-teste"), auth.loginAttempts)
        }
        composeRule.onNodeWithText("Entrar como Visitante").assertDoesNotExist()
    }

    private fun createViewModel(auth: RecordingProfileAuthRepository) = UserProfileViewModel(
        authRepository = auth,
        sessionRepository = RecordingProfileSessionRepository,
        getUserProfileUseCase = GetUserProfileUseCase(auth),
        logoutUseCase = LogoutUseCase(auth),
        switchUserUseCase = SwitchUserUseCase(auth),
        context = InstrumentationRegistry.getInstrumentation().targetContext,
    )
}

private class RecordingProfileAuthRepository : AuthRepository {
    private val logoutCallCount = AtomicInteger()
    val logoutCalls: Int get() = logoutCallCount.get()
    val loginAttempts = CopyOnWriteArrayList<Pair<String, String>>()
    override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Servidor de teste", "1.0"))
    override suspend fun register(username: String, password: String) = Result.failure<RegistrationResult>(UnsupportedOperationException())
    override suspend fun login(username: String, password: String): Result<UserSession> {
        loginAttempts += username to password
        return Result.success(UserSession("guest-id", username, "guest-token", "test-server"))
    }
    override suspend fun getAvailableUsers() = Result.success(
        listOf(
            AvailableUser("current-id", "Usuário atual"),
            AvailableUser("guest-id", "Visitante"),
        ),
    )
    override suspend fun initiateQuickConnect() = Result.failure<QuickConnectState>(UnsupportedOperationException())
    override suspend fun checkQuickConnect(secret: String) = Result.success<UserSession?>(null)
    override suspend fun logout(): Result<Unit> {
        logoutCallCount.incrementAndGet()
        return Result.success(Unit)
    }
    override suspend fun getCurrentUserProfile() = Result.success(
        UserProfile("current-id", "Usuário atual", isAdministrator = false),
    )
    override fun getSavedServerUrl(): Flow<String> = flowOf("https://example.invalid")
    override suspend fun setServerUrl(url: String) = Unit
    override fun getSavedUserId(): Flow<String?> = flowOf("current-id")
    override fun getSavedUserName(): Flow<String?> = flowOf("Usuário atual")
    override fun getSavedToken(): Flow<String?> = flowOf("current-token")
}

private object RecordingProfileSessionRepository : SessionRepository {
    override fun getAccessToken(): Flow<String?> = flowOf("current-token")
    override fun getDeviceId(): Flow<String> = flowOf("test-device")
    override fun getBaseUrl(): Flow<String> = flowOf("https://example.invalid")
    override fun getCurrentUserId(): Flow<String?> = flowOf("current-id")
    override fun getCurrentUserName(): Flow<String?> = flowOf("Usuário atual")
    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
    override suspend fun setBaseUrl(url: String) = Unit
    override suspend fun clearSession() = Unit
}
