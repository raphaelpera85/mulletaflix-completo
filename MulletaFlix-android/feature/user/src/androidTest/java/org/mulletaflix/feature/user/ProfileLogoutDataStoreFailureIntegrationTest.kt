package org.mulletaflix.feature.user

import android.content.Context
import android.content.ContextWrapper
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import java.io.File
import java.util.UUID
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.data.repository.AuthRepositoryImpl
import org.mulletaflix.data.repository.SessionRepositoryImpl
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.usecase.GetUserProfileUseCase
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.SwitchUserUseCase
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

@RunWith(AndroidJUnit4::class)
class ProfileLogoutDataStoreFailureIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun failedRealDataStoreLogoutShowsErrorAndKeepsProfileVisible() {
        val targetContext = InstrumentationRegistry.getInstrumentation().targetContext
        val isolatedPath = File(targetContext.cacheDir, "profile-logout-${UUID.randomUUID()}")
        val isolatedFilesDir = File(isolatedPath, "files")
        check(isolatedFilesDir.mkdirs())
        val isolatedContext = object : ContextWrapper(targetContext) {
            override fun getApplicationContext(): Context = this
            override fun getFilesDir(): File = isolatedFilesDir
        }
        val sessionRepository = SessionRepositoryImpl(isolatedContext)
        val dataStoreDirectory = File(isolatedFilesDir, "datastore")
        val server = MockWebServer()
        server.start()

        try {
            runBlocking {
                sessionRepository.saveSession(
                    serverUrl = server.url("/").toString().trimEnd('/'),
                    token = "synthetic-profile-logout-token",
                    userId = "profile-user",
                    userName = "Conta de teste",
                    serverId = "profile-server",
                    deviceId = "profile-device",
                )
            }
            assertTrue(dataStoreDirectory.isDirectory)
            assertTrue(dataStoreDirectory.setWritable(false, false))
            assertFalse("DataStore must reject writes before the UI logout", dataStoreDirectory.canWrite())

            val api = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(
                    MoshiConverterFactory.create(Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()),
                )
                .build()
                .create(MulletaFlixApiService::class.java)
            val realAuthRepository = AuthRepositoryImpl(api, sessionRepository)
            val authRepository = ProfileTestAuthRepository(realAuthRepository)
            val viewModel = UserProfileViewModel(
                authRepository = authRepository,
                sessionRepository = sessionRepository,
                getUserProfileUseCase = GetUserProfileUseCase(authRepository),
                logoutUseCase = LogoutUseCase(authRepository),
                switchUserUseCase = SwitchUserUseCase(authRepository),
                context = isolatedContext,
            )
            var logoutNavigations = 0

            composeRule.setContent {
                MaterialTheme {
                    ProfileScreen(viewModel = viewModel, onLogout = { logoutNavigations++ })
                }
            }
            composeRule.onNodeWithText("Sair da Conta").performScrollTo().performClick()
            composeRule.onNodeWithText("Sair").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                viewModel.uiState.value.error != null && !viewModel.uiState.value.isLoggingOut
            }

            val error = requireNotNull(viewModel.uiState.value.error)
            assertTrue("logout error must be meaningful", error.isNotBlank())
            composeRule.onNodeWithText("Meu Perfil").assertIsDisplayed()
            composeRule.onNodeWithText(error).assertIsDisplayed()
            composeRule.runOnIdle {
                assertFalse(viewModel.uiState.value.isLoggingOut)
                assertEquals(0, logoutNavigations)
            }
            assertEquals("logout must not make a media-server request", 0, server.requestCount)

            assertTrue(dataStoreDirectory.setWritable(true, true))
            assertEquals("failed logout must preserve the persisted account", "synthetic-profile-logout-token", runBlocking {
                sessionRepository.getAccessToken().first()
            })
        } finally {
            try {
                dataStoreDirectory.setWritable(true, true)
                runBlocking { sessionRepository.clearSession() }
            } finally {
                try {
                    server.shutdown()
                } finally {
                    isolatedPath.deleteRecursively()
                }
            }
        }
    }
}

private class ProfileTestAuthRepository(
    private val realRepository: AuthRepositoryImpl,
) : AuthRepository by realRepository {
    override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Servidor de teste", "1.0"))
    override suspend fun getAvailableUsers() = Result.success(emptyList<AvailableUser>())
    override suspend fun getCurrentUserProfile() = Result.success(
        UserProfile(id = "profile-user", name = "Conta de teste"),
    )
}
