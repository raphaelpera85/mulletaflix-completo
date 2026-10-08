package org.mulletaflix.feature.home

import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.getOrNull
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performScrollToNode
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import androidx.lifecycle.ViewModelStore
import androidx.navigation.NavController
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import org.junit.Assert.assertTrue
import org.junit.Assert.assertEquals
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.designsystem.components.isTelevisionDevice
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.GetHomeFeedUseCase
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger

@RunWith(AndroidJUnit4::class)
class TvHomeRefreshIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun booksLibraryFromLoadedFeedIsHiddenByRealTvHomeScreen() {
        val expectedProfile = requireNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE, TABLET or TV for this AVD run" }
        assertEquals("This visibility check must run on the TV profile", "TV", expectedProfile)

        val owner = TestLifecycleOwner()
        val booksLibrary = MediaItem(
            "books-library",
            "Livros",
            MediaItemType.CollectionFolder,
            collectionType = "books",
        )
        val moviesLibrary = MediaItem(
            "movies-library",
            "Filmes",
            MediaItemType.CollectionFolder,
            collectionType = "movies",
        )
        val resumeMovie = MediaItem("resume-movie", "Filme em andamento", MediaItemType.Movie)
        val resumeBook = MediaItem("resume-book", "Livro em andamento", MediaItemType.Book)
        val nextEpisode = MediaItem("next-episode", "Episódio seguinte", MediaItemType.Episode)
        val nextBook = MediaItem("next-book", "Livro no próximo episódio", MediaItemType.Book)
        val favoriteMovie = MediaItem("favorite-movie", "Filme favorito", MediaItemType.Movie)
        val favoriteBook = MediaItem("favorite-book", "Livro favorito", MediaItemType.Book)
        val recentMovie = MediaItem("recent-movie", "Filme recém-adicionado", MediaItemType.Movie)
        val recentBookInMovies = MediaItem("recent-book-mixed", "Livro misturado em filmes", MediaItemType.Book)
        val recentBook = MediaItem("recent-book", "Livro recentemente adicionado", MediaItemType.Book)
        val mediaRepository = object : EmptyMediaRepository() {
            override suspend fun getLibraries(userId: String) =
                Result.success(listOf(moviesLibrary, booksLibrary))

            override suspend fun getResumeItems(userId: String, limit: Int) =
                Result.success(listOf(resumeMovie, resumeBook))

            override suspend fun getNextUp(userId: String, limit: Int) =
                Result.success(listOf(nextEpisode, nextBook))

            override suspend fun getItems(
                userId: String,
                parentId: String?,
                includeItemTypes: String?,
                sortBy: String?,
                sortOrder: String?,
                filters: String?,
                searchTerm: String?,
                startIndex: Int,
                limit: Int,
                genres: String?,
                years: String?,
                officialRatings: String?,
                isPlayed: Boolean?,
                isFavorite: Boolean?,
            ) = Result.success(listOf(favoriteMovie, favoriteBook) to 2)

            override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) =
                Result.success(
                    when (parentId) {
                        moviesLibrary.id -> listOf(recentMovie, recentBookInMovies)
                        booksLibrary.id -> listOf(recentBook)
                        else -> emptyList()
                    },
                )
        }
        val viewModelStore = ViewModelStore()
        val viewModel = HomeViewModel(
            getHomeFeedUseCase = GetHomeFeedUseCase(mediaRepository),
            sessionRepository = TestSessionRepository(),
            networkMonitor = object : NetworkMonitor { override val isOnline = flowOf(true) },
            authRepository = TestAuthRepository(),
        )
        val renderedOnTv = AtomicBoolean(false)
        composeRule.runOnUiThread { viewModelStore.put("tv-home-books-visibility", viewModel) }

        try {
            composeRule.runOnUiThread {
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
            }
            composeRule.setContent {
                renderedOnTv.set(isTelevisionDevice())
                CompositionLocalProvider(androidx.lifecycle.compose.LocalLifecycleOwner provides owner) {
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
            }
            composeRule.waitForIdle()
            assumeTrue("This integration scenario must run on Android TV", renderedOnTv.get())
            composeRule.waitUntil(timeoutMillis = 10_000) {
                val state = viewModel.state.value
                !state.isLoading &&
                    booksLibrary in state.libraries &&
                    state.resumeItems == listOf(resumeMovie, resumeBook) &&
                    state.nextUpItems == listOf(nextEpisode, nextBook) &&
                    state.favoriteItems == listOf(favoriteMovie, favoriteBook) &&
                    state.recentlyAddedByLibrary[moviesLibrary.id] == listOf(recentMovie, recentBookInMovies) &&
                    state.recentlyAddedByLibrary[booksLibrary.id] == listOf(recentBook)
            }

            assertEquals(listOf(resumeMovie), homeMediaItemsForDevice(viewModel.state.value.resumeItems, true))
            assertEquals(listOf(nextEpisode), homeMediaItemsForDevice(viewModel.state.value.nextUpItems, true))
            assertEquals(listOf(favoriteMovie), homeMediaItemsForDevice(viewModel.state.value.favoriteItems, true))
            assertEquals(
                listOf(recentMovie),
                homeRecentLibrarySections(
                    libraries = viewModel.state.value.libraries,
                    recentItemsByLibraryId = viewModel.state.value.recentlyAddedByLibrary,
                    errorsByLibraryId = viewModel.state.value.recentlyAddedErrorsByLibrary,
                    isTelevision = true,
                ).first { it.library.id == moviesLibrary.id }.items,
            )

            val homeFeed = composeRule.onNodeWithTag(HOME_FEED_TEST_TAG)
            listOf(
                resumeMovie to resumeBook,
                nextEpisode to nextBook,
                favoriteMovie to favoriteBook,
                recentMovie to recentBookInMovies,
            ).forEach { (visibleTitle, hiddenBook) ->
                homeFeed.performScrollToNode(hasContentDescription("Abrir ${visibleTitle.name}"))
                composeRule.onNodeWithContentDescription("Abrir ${visibleTitle.name}").assertIsDisplayed()
                composeRule.onNodeWithContentDescription("Abrir ${hiddenBook.name}").assertDoesNotExist()
            }
            homeFeed.performScrollToNode(hasContentDescription("Abrir Filmes"))
            composeRule.onNodeWithContentDescription("Abrir Filmes").assertIsDisplayed()
            composeRule.onNodeWithContentDescription("Abrir Livros").assertDoesNotExist()
        } finally {
            composeRule.runOnUiThread {
                if (owner.lifecycle.currentState != Lifecycle.State.DESTROYED) {
                    owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_DESTROY)
                }
                viewModelStore.clear()
            }
        }
    }

    @Test
    fun returning_to_tv_home_replaces_visible_media_with_refreshed_feed() {
        val owner = TestLifecycleOwner()
        val firstTitle = MediaItem("old", "Título antigo", MediaItemType.Movie)
        val refreshedTitle = MediaItem("new", "Título atualizado", MediaItemType.Movie)
        val refreshResponse = CompletableDeferred<Result<List<MediaItem>>>()
        val resumeCalls = AtomicInteger()
        val refreshStarted = AtomicBoolean(false)
        val mediaRepository = object : EmptyMediaRepository() {
            override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> =
                if (resumeCalls.incrementAndGet() == 1) {
                    Result.success(listOf(firstTitle))
                } else {
                    refreshStarted.set(true)
                    refreshResponse.await()
                }
        }
        val viewModelStore = ViewModelStore()
        val viewModel = HomeViewModel(
            getHomeFeedUseCase = GetHomeFeedUseCase(mediaRepository),
            sessionRepository = TestSessionRepository(),
            networkMonitor = object : NetworkMonitor { override val isOnline = flowOf(true) },
            authRepository = TestAuthRepository(),
        )
        val renderedOnTv = AtomicBoolean(false)
        composeRule.runOnUiThread { viewModelStore.put("tv-home-refresh", viewModel) }

        try {
            composeRule.runOnUiThread {
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
            }
            composeRule.setContent {
                renderedOnTv.set(isTelevisionDevice())
                CompositionLocalProvider(androidx.lifecycle.compose.LocalLifecycleOwner provides owner) {
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
            }
            composeRule.waitForIdle()
            assumeTrue("This integration scenario must run on Android TV", renderedOnTv.get())

            composeRule.waitUntil(timeoutMillis = 10_000) {
                !viewModel.state.value.isLoading && viewModel.state.value.resumeItems == listOf(firstTitle)
            }
            composeRule.onNode(heroTitleMatcher(firstTitle.name))
                .performScrollTo()
                .assertIsDisplayed()

            composeRule.runOnUiThread {
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
            }
            composeRule.waitForIdle()
            composeRule.runOnUiThread {
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
                owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
            }

            composeRule.waitUntil(timeoutMillis = 10_000) { refreshStarted.get() }
            composeRule.onNode(heroTitleMatcher(firstTitle.name))
                .performScrollTo()
                .assertIsDisplayed()
            refreshResponse.complete(Result.success(listOf(refreshedTitle)))

            composeRule.waitUntil(timeoutMillis = 10_000) {
                viewModel.state.value.resumeItems == listOf(refreshedTitle)
            }
            composeRule.onNode(heroTitleMatcher(refreshedTitle.name))
                .performScrollTo()
                .assertIsDisplayed()
            assertTrue(
                "The old hero title must leave the refreshed Home semantics tree",
                composeRule.onAllNodes(heroTitleMatcher(firstTitle.name)).fetchSemanticsNodes().isEmpty(),
            )
            assertTrue("The resumed TV screen must issue a fresh catalog request", resumeCalls.get() >= 2)
        } finally {
            if (!refreshResponse.isCompleted) refreshResponse.complete(Result.success(emptyList()))
            composeRule.runOnUiThread {
                if (owner.lifecycle.currentState != Lifecycle.State.DESTROYED) {
                    owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_DESTROY)
                }
                viewModelStore.clear()
            }
        }
    }

    private fun heroTitleMatcher(title: String) =
        hasText(title, substring = true) and SemanticsMatcher("hero title without a content description") {
            it.config.getOrNull(SemanticsProperties.ContentDescription).isNullOrEmpty()
        }

    private class TestLifecycleOwner : LifecycleOwner {
        val registry = LifecycleRegistry(this)
        override val lifecycle: Lifecycle = registry
    }

    private class TestSessionRepository : SessionRepository {
        private val userId = MutableStateFlow<String?>("tv-test-user")
        override fun getAccessToken() = flowOf<String?>(null)
        override fun getDeviceId() = flowOf("tv-test-device")
        override fun getBaseUrl() = flowOf("http://127.0.0.1:8096")
        override fun getCurrentUserId(): Flow<String?> = userId
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() { userId.value = null }
    }

    private class TestAuthRepository : AuthRepository {
        override suspend fun verifyServer(url: String) = Result.success(ServerVerification("TV test", "test"))
        override suspend fun register(username: String, password: String) = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String) = Result.success(UserSession("tv-test-user", username, "token", null))
        override suspend fun getAvailableUsers() = Result.success(emptyList<AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.success(QuickConnectState("000000", "secret", false))
        override suspend fun checkQuickConnect(secret: String) = Result.success<UserSession?>(null)
        override suspend fun logout() = Result.success(Unit)
        override suspend fun getCurrentUserProfile() = Result.failure<UserProfile>(UnsupportedOperationException())
        override fun getSavedServerUrl() = flowOf("http://127.0.0.1:8096")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId() = flowOf<String?>("tv-test-user")
        override fun getSavedToken() = flowOf<String?>(null)
    }

    private open class EmptyMediaRepository : MediaRepository {
        override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, officialRatings: String?, isPlayed: Boolean?, isFavorite: Boolean?) = Result.success(emptyList<MediaItem>() to 0)
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
