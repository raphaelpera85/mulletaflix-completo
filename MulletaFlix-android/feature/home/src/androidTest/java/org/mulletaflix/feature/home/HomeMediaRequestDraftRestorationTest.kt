package org.mulletaflix.feature.home

import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.navigation.NavController
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.LibraryFilterOptions
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.domain.usecase.GetHomeFeedUseCase
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf

class HomeMediaRequestDraftRestorationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun requestDraftSurvivesSavedInstanceStateRestoration() {
        val viewModel = HomeViewModel(
            getHomeFeedUseCase = GetHomeFeedUseCase(EmptyMediaRepository()),
            sessionRepository = TestSessionRepository(),
            networkMonitor = object : NetworkMonitor { override val isOnline = flowOf(true) },
            authRepository = TestAuthRepository(),
            userFeedbackRepository = object : UserFeedbackRepository {
                override suspend fun requestMedia(
                    session: FeedbackRequestSession,
                    title: String,
                    mediaType: String,
                    year: Int?,
                    notes: String?,
                ) = Result.success(Unit)

                override suspend fun reportPlaybackIssue(
                    session: FeedbackRequestSession,
                    itemId: String,
                    category: String,
                    description: String?,
                ) = Result.success(Unit)
            },
        )
        val restorationTester = StateRestorationTester(composeRule)

        restorationTester.setContent {
            val context = LocalContext.current
            val navController = remember(context) { NavController(context) }
            MulletaFlixTheme {
                HomeScreen(
                    onItemClick = {},
                    onPlayItemClick = {},
                    onLibraryClick = {},
                    onLiveTvClick = {},
                    navController = navController,
                    viewModel = viewModel,
                )
            }
        }

        composeRule.onNodeWithContentDescription("Solicitar mídia").performClick()
        composeRule.onNodeWithTag("media-request-title").performTextInput("Duna")
        composeRule.onNodeWithText("Série").performClick()
        composeRule.onNodeWithText("Filme").performClick()
        composeRule.onNodeWithTag("media-request-year").performTextInput("2024")
        composeRule.onNodeWithTag("media-request-notes").performScrollTo().performTextInput("Legendas em português")

        restorationTester.emulateSavedInstanceStateRestore()

        composeRule.onNodeWithContentDescription("Solicitar mídia").performClick()
        composeRule.onNodeWithTag("media-request-title").assertTextContains("Duna")
        composeRule.onNodeWithText("Filme").assertExists()
        composeRule.onNodeWithTag("media-request-year").assertTextContains("2024")
        composeRule.onNodeWithTag("media-request-notes").performScrollTo().assertTextContains("Legendas em português")
    }

    private class TestSessionRepository : SessionRepository {
        override fun getAccessToken() = flowOf("request-token")
        override fun getDeviceId() = flowOf("request-device")
        override fun getBaseUrl() = flowOf("https://request.test")
        override fun getCurrentUserId() = flowOf("request-user")
        override fun getFeedbackRequestSession() = flowOf(
            FeedbackRequestSession("https://request.test", "request-token", "request-user", "request-device"),
        )
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
        override fun getSavedServerUrl() = flowOf("https://request.test")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId() = flowOf("request-user")
        override fun getSavedToken() = flowOf("request-token")
    }

    private class EmptyMediaRepository : MediaRepository {
        override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getItems(
            userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?,
            filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?,
            officialRatings: String?, isPlayed: Boolean?, isFavorite: Boolean?,
        ) = Result.success(emptyList<MediaItem>() to 0)
        override suspend fun getLibraryFilterOptions(userId: String, parentId: String, includeItemTypes: String?) =
            Result.failure<LibraryFilterOptions>(UnsupportedOperationException())
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
