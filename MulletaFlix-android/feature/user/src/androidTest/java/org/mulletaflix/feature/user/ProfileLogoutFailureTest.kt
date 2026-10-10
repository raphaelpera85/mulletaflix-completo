package org.mulletaflix.feature.user

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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

@RunWith(AndroidJUnit4::class)
class ProfileLogoutFailureTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun failedLogoutRendersErrorAndDoesNotInvokeNavigation() {
        val logoutError = "Armazenamento da sessão indisponível"
        var logoutNavigations = 0
        val authRepository = FailingLogoutAuthRepository(IllegalStateException(logoutError))
        val viewModel = UserProfileViewModel(
            authRepository = authRepository,
            sessionRepository = TestProfileSessionRepository(),
            getUserProfileUseCase = GetUserProfileUseCase(authRepository),
            logoutUseCase = LogoutUseCase(authRepository),
            switchUserUseCase = SwitchUserUseCase(authRepository),
            context = InstrumentationRegistry.getInstrumentation().targetContext,
        )

        composeRule.setContent {
            MaterialTheme {
                ProfileScreen(viewModel = viewModel, onLogout = { logoutNavigations++ })
            }
        }

        composeRule.runOnIdle { viewModel.logout { logoutNavigations++ } }

        composeRule.waitUntil(timeoutMillis = 5_000) {
            viewModel.uiState.value.error == logoutError && !viewModel.uiState.value.isLoggingOut
        }
        composeRule.onNodeWithText(logoutError).assertExists()
        composeRule.runOnIdle { assertEquals(0, logoutNavigations) }
        composeRule.runOnIdle { assertFalse(viewModel.uiState.value.isLoggingOut) }
    }
}

private class FailingLogoutAuthRepository(
    private val logoutFailure: Throwable,
) : AuthRepository {
    override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Teste", "1.0"))
    override suspend fun register(username: String, password: String) = Result.failure<RegistrationResult>(UnsupportedOperationException())
    override suspend fun login(username: String, password: String) = Result.failure<UserSession>(UnsupportedOperationException())
    override suspend fun getAvailableUsers() = Result.success(emptyList<AvailableUser>())
    override suspend fun initiateQuickConnect() = Result.failure<QuickConnectState>(UnsupportedOperationException())
    override suspend fun checkQuickConnect(secret: String) = Result.success<UserSession?>(null)
    override suspend fun logout(): Result<Unit> = Result.failure(logoutFailure)
    override suspend fun getCurrentUserProfile() = Result.failure<UserProfile>(UnsupportedOperationException())
    override fun getSavedServerUrl(): Flow<String> = flowOf("https://mulletaflix.duckdns.org")
    override suspend fun setServerUrl(url: String) = Unit
    override fun getSavedUserId(): Flow<String?> = flowOf("test-user")
    override fun getSavedUserName(): Flow<String?> = flowOf("Usuário de teste")
    override fun getSavedToken(): Flow<String?> = flowOf("test-token")
}

private class TestProfileSessionRepository : SessionRepository {
    override fun getAccessToken(): Flow<String?> = flowOf("test-token")
    override fun getDeviceId(): Flow<String> = flowOf("test-device")
    override fun getBaseUrl(): Flow<String> = flowOf("https://mulletaflix.duckdns.org")
    override fun getCurrentUserId(): Flow<String?> = flowOf("test-user")
    override fun getCurrentUserName(): Flow<String?> = flowOf("Usuário de teste")
    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
    override suspend fun setBaseUrl(url: String) = Unit
    override suspend fun clearSession() = Unit
}
