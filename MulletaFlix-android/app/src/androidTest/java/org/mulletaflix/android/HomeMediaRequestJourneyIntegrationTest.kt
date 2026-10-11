package org.mulletaflix.android

import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.navigation.NavController
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.ServerUrlInterceptor
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.data.repository.UserFeedbackRepositoryImpl
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.LibraryFilterOptions
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.usecase.GetHomeFeedUseCase
import org.mulletaflix.feature.home.HomeScreen
import org.mulletaflix.feature.home.HomeViewModel
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

class HomeMediaRequestJourneyIntegrationTest {
    @get:Rule val composeRule = createComposeRule()
    private val server = MockWebServer()

    @After fun closeServer() = server.shutdown()

    @Test
    fun suggestionSelectionSubmitsAuthenticatedRequestWithSelectedMetadata() {
        server.start()
        server.enqueue(MockResponse().setResponseCode(200).setBody("""[{"Title":"Duna: Parte Dois","MediaType":"Movie","Year":2024}]"""))
        server.enqueue(MockResponse().setResponseCode(204))
        val session = FeedbackRequestSession(server.url("/").toString().trimEnd('/'), "journey-token", "journey-user", "journey-device")
        val sessions = FixedSessionRepository(session)
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient.Builder().addInterceptor(ServerUrlInterceptor(sessions)).addInterceptor(ClientIdentityInterceptor(sessions)).build())
            .addConverterFactory(MoshiConverterFactory.create(Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()))
            .build().create(MulletaFlixApiService::class.java)
        val viewModel = HomeViewModel(
            GetHomeFeedUseCase(EmptyMediaRepository()), sessions,
            object : NetworkMonitor { override val isOnline = flowOf(true) }, TestAuthRepository(),
            UserFeedbackRepositoryImpl(api),
        )

        composeRule.setContent {
            val context = LocalContext.current
            val navController = remember(context) { NavController(context) }
            MulletaFlixTheme {
                HomeScreen({}, {}, {}, {}, navController = navController, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(5_000) { viewModel.state.value.feedbackSessionLoaded }
        composeRule.onNodeWithContentDescription("Solicitar mídia").performClick()
        composeRule.onNodeWithTag("media-request-title").performTextInput("Duna")
        composeRule.waitUntil(10_000) {
            composeRule.onAllNodesWithText("Duna: Parte Dois").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Duna: Parte Dois").performScrollTo().performClick()
        composeRule.onNodeWithTag("media-request-notes").performScrollTo().performTextInput("Áudio e legendas em português")
        composeRule.onNodeWithText("Enviar").performClick()

        val suggestionRequest = server.takeRequest(10, java.util.concurrent.TimeUnit.SECONDS)
        val submission = server.takeRequest(10, java.util.concurrent.TimeUnit.SECONDS)
        assertTrue("Suggestion request should reach the authenticated catalog", suggestionRequest != null)
        assertTrue("Submission should reach the authenticated media-request endpoint", submission != null)
        assertEquals("GET", suggestionRequest!!.method)
        assertEquals("/UserFeedback/MediaSuggestions", suggestionRequest.requestUrl?.encodedPath)
        assertEquals("Duna", suggestionRequest.requestUrl?.queryParameter("query"))
        assertEquals("POST", submission!!.method)
        assertEquals("/UserFeedback/MediaRequests", submission.requestUrl?.encodedPath)
        assertEquals(
            """{"Title":"Duna: Parte Dois","MediaType":"Filme","Year":2024,"Notes":"Áudio e legendas em português"}""",
            submission.body.readUtf8(),
        )
        composeRule.waitUntil(5_000) {
            composeRule.onAllNodesWithText("Solicitar mídia").fetchSemanticsNodes().isEmpty()
        }
        listOf(suggestionRequest, submission).forEach { request ->
            val identity = request.getHeader("Authorization").orEmpty()
            assertTrue(identity, identity.contains("Token=\"journey-token\""))
            assertTrue(identity, identity.contains("DeviceId=\"journey-device\""))
        }
    }

    private class FixedSessionRepository(private val session: FeedbackRequestSession) : SessionRepository {
        override fun getAccessToken() = flowOf(session.accessToken)
        override fun getDeviceId() = flowOf(session.deviceId)
        override fun getBaseUrl() = flowOf(session.serverUrl)
        override fun getCurrentUserId() = flowOf(session.userId)
        override fun getFeedbackRequestSession() = flowOf(session)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private class TestAuthRepository : AuthRepository {
        override suspend fun verifyServer(url: String) = Result.failure<org.mulletaflix.domain.repository.ServerVerification>(UnsupportedOperationException())
        override suspend fun register(username: String, password: String) = Result.failure<org.mulletaflix.domain.repository.RegistrationResult>(UnsupportedOperationException())
        override suspend fun login(username: String, password: String) = Result.failure<org.mulletaflix.domain.repository.UserSession>(UnsupportedOperationException())
        override suspend fun getAvailableUsers() = Result.success(emptyList<org.mulletaflix.domain.repository.AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.failure<org.mulletaflix.domain.repository.QuickConnectState>(UnsupportedOperationException())
        override suspend fun checkQuickConnect(secret: String) = Result.success<org.mulletaflix.domain.repository.UserSession?>(null)
        override suspend fun logout() = Result.success(Unit)
        override suspend fun getCurrentUserProfile() = Result.failure<UserProfile>(UnsupportedOperationException())
        override fun getSavedServerUrl() = flowOf("")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId() = flowOf(null)
        override fun getSavedToken() = flowOf(null)
    }

    private class EmptyMediaRepository : MediaRepository {
        override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, officialRatings: String?, isPlayed: Boolean?, isFavorite: Boolean?) = Result.success(emptyList<MediaItem>() to 0)
        override suspend fun getLibraryFilterOptions(userId: String, parentId: String, includeItemTypes: String?) = Result.failure<LibraryFilterOptions>(UnsupportedOperationException())
        override suspend fun getItem(userId: String, itemId: String) = Result.failure<MediaItem>(UnsupportedOperationException())
        override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getSeasons(userId: String, seriesId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?) = Result.success(emptyList<MediaItem>())
        override suspend fun getSpecialFeatures(userId: String, itemId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun markAsPlayed(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun markAsUnplayed(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun markAsFavorite(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun unmarkAsFavorite(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun getLiveTvChannelPreview(userId: String) = Result.success(emptyList<MediaItem>())
    }
}
