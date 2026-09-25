package org.mulletaflix.feature.library

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.withContext
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.GetFavoriteItemsUseCase
import org.mulletaflix.core.common.network.NetworkMonitor

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class FavoritesViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var media: FakeMediaRepository

    @Before fun setUp() {
        Dispatchers.setMain(dispatcher)
        media = FakeMediaRepository()
    }

    @After fun tearDown() = Dispatchers.resetMain()

    private fun createViewModel(networkMonitor: NetworkMonitor = FakeNetworkMonitor()) = FavoritesViewModel(
        getFavoriteItemsUseCase = GetFavoriteItemsUseCase(media),
        authRepository = FakeAuthRepository(),
        settingsRepository = FakeSettingsRepository(),
        networkMonitor = networkMonitor,
    )

    @Test
    fun `loads favorites with server filter and supports pagination`() = runTest {
        val first = MediaItem("one", "One", MediaItemType.Movie)
        val second = MediaItem("two", "Two", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        media.pages[1] = Result.success(listOf(second) to 2)
        val viewModel = createViewModel()

        advanceUntilIdle()
        assertEquals(listOf(first), viewModel.state.value.items)
        assertEquals("IsFavorite", media.lastFilters)
        assertEquals(true, media.lastIsFavorite)
        assertTrue(viewModel.state.value.hasMore)

        viewModel.loadMore()
        advanceUntilIdle()
        assertEquals(listOf(first, second), viewModel.state.value.items)
        assertEquals(1, media.lastStartIndex)
        assertTrue(!viewModel.state.value.hasMore)
    }

    /**
     * The server can insert or remove a favourite between two requests, which shifts
     * the offset window: the tail of page 1 comes back as the head of page 2. The grid
     * renders `key = item.id`, and the offset has to keep advancing past every item the
     * server handed over — duplicates included — or the same window is requested
     * forever.
     */
    @Test
    fun `a shifted page does not repeat a favorite and the offset keeps advancing`() = runTest {
        val a = MediaItem("a", "A", MediaItemType.Movie)
        val b = MediaItem("b", "B", MediaItemType.Movie)
        val c = MediaItem("c", "C", MediaItemType.Movie)
        val d = MediaItem("d", "D", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(a, b, c) to 10)
        val viewModel = createViewModel()
        advanceUntilIdle()

        media.pages[3] = Result.success(listOf(c, d) to 10)
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(
            "the repeated favorite must not be rendered twice",
            listOf("a", "b", "c", "d"),
            viewModel.state.value.items.map { it.id },
        )

        media.pages[5] = Result.success(emptyList<MediaItem>() to 10)
        viewModel.loadMore()
        advanceUntilIdle()
        assertEquals(
            "the next offset counts fetched items, not the deduplicated list",
            5,
            media.lastStartIndex,
        )
    }

    @Test
    fun `pagination failure preserves loaded favorites and exposes retryable error`() = runTest {
        val first = MediaItem("one", "One", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        media.pages[1] = Result.failure(IllegalStateException("network"))
        val viewModel = createViewModel()

        advanceUntilIdle()
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(listOf(first), viewModel.state.value.items)
        assertTrue(viewModel.state.value.error?.contains("network") == true)
        assertTrue(viewModel.state.value.hasMore)
    }

    @Test
    fun `missing session finishes loading and exposes reauthentication state`() = runTest {
        val auth = FakeAuthRepository().apply { userIdState.value = null }
        val viewModel = FavoritesViewModel(GetFavoriteItemsUseCase(media), auth, FakeSettingsRepository(), FakeNetworkMonitor())

        advanceUntilIdle()

        assertEquals(false, viewModel.state.value.isLoading)
        assertEquals(false, viewModel.state.value.isRefreshing)
        assertEquals("Sessão expirada. Entre novamente.", viewModel.state.value.error)
    }

    @Test
    fun `repeated load more taps start only one page request`() = runTest {
        val first = MediaItem("one", "One", MediaItemType.Movie)
        val second = MediaItem("two", "Two", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        media.pages[1] = Result.success(listOf(second) to 2)
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadMore()
        viewModel.loadMore()
        runCurrent()

        assertEquals(1, media.pageRequestCountFor(1))
    }

    @Test
    fun `idle refresh does not cancel or duplicate an in flight request`() = runTest {
        val response = CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>()
        media.responseSequence = ArrayDeque(listOf(response))
        val viewModel = createViewModel()
        runCurrent()

        viewModel.refreshIfIdle()
        viewModel.refreshIfIdle()
        runCurrent()

        assertEquals(1, media.pageRequestCountFor(0))
        response.complete(Result.success(emptyList<MediaItem>() to 0))
        advanceUntilIdle()
    }

    @Test
    fun `late refresh response cannot replace a newer favorites list`() = runTest {
        val oldResponse = CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>()
        val old = MediaItem("old", "Old", MediaItemType.Movie)
        val fresh = MediaItem("fresh", "Fresh", MediaItemType.Movie)
        media.responseSequence = ArrayDeque(listOf(oldResponse, CompletableDeferred(Result.success(listOf(fresh) to 1))))
        val viewModel = createViewModel()
        runCurrent()

        viewModel.refresh()
        runCurrent()
        assertEquals(listOf(fresh), viewModel.state.value.items)

        oldResponse.complete(Result.success(listOf(old) to 1))
        advanceUntilIdle()
        assertEquals(listOf(fresh), viewModel.state.value.items)
    }

    @Test
    fun `refreshes favorites when network returns after an outage`() = runTest {
        val stale = MediaItem("stale", "Stale", MediaItemType.Movie)
        val fresh = MediaItem("fresh", "Fresh", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(stale) to 1)
        val network = FakeNetworkMonitor(initialOnline = false)
        val viewModel = createViewModel(network)

        advanceUntilIdle()
        assertEquals(listOf(stale), viewModel.state.value.items)
        assertEquals(true, viewModel.state.value.isOffline)
        media.pages[0] = Result.success(listOf(fresh) to 1)

        network.setOnline(true)
        advanceUntilIdle()

        assertEquals(listOf(fresh), viewModel.state.value.items)
        assertEquals(false, viewModel.state.value.isOffline)
        assertEquals(2, media.pageRequestCountFor(0))
    }

    @Test
    fun `late favorites response from a previous user cannot replace current session`() = runTest {
        val oldResponse = CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>()
        val old = MediaItem("old", "Conta antiga", MediaItemType.Movie)
        media.responseSequence = ArrayDeque(listOf(oldResponse))
        val auth = FakeAuthRepository()
        val viewModel = FavoritesViewModel(GetFavoriteItemsUseCase(media), auth, FakeSettingsRepository(), FakeNetworkMonitor())
        runCurrent()

        auth.userIdState.value = "user-2"
        runCurrent()
        advanceUntilIdle()

        oldResponse.complete(Result.success(listOf(old) to 1))
        advanceUntilIdle()

        assertTrue(viewModel.state.value.items.isEmpty())
        assertEquals(false, viewModel.state.value.hasMore)
    }

    private class FakeNetworkMonitor(initialOnline: Boolean = true) : NetworkMonitor {
        private val online = MutableStateFlow(initialOnline)
        override val isOnline: Flow<Boolean> = online

        fun setOnline(value: Boolean) {
            online.value = value
        }
    }

    private class FakeMediaRepository : MediaRepository {
        val pages = mutableMapOf<Int, Result<Pair<List<MediaItem>, Int>>>()
        var lastFilters: String? = null
        var lastIsFavorite: Boolean? = null
        var lastStartIndex = -1
        var requestStarts = mutableListOf<Int>()
        var responseSequence: ArrayDeque<CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>>? = null

        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> {
            lastFilters = filters
            lastIsFavorite = isFavorite
            lastStartIndex = startIndex
            requestStarts += startIndex
            responseSequence?.removeFirstOrNull()?.let { deferred ->
                return withContext(NonCancellable) { deferred.await() }
            }
            return pages[startIndex] ?: Result.success(emptyList<MediaItem>() to 0)
        }

        fun pageRequestCountFor(startIndex: Int): Int = requestStarts.count { it == startIndex }
        override suspend fun getItem(userId: String, itemId: String) = Result.success(MediaItem(itemId, itemId, MediaItemType.Movie))
        override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
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

    private class FakeAuthRepository : AuthRepository {
        val userIdState = MutableStateFlow<String?>("user-1")
        override fun getSavedUserId() = userIdState
        override fun getSavedToken() = MutableStateFlow<String?>("token")
        override fun getSavedServerUrl() = MutableStateFlow("http://localhost")
        override suspend fun verifyServer(url: String) = Result.success(ServerVerification("Test", "1"))
        override suspend fun register(username: String, password: String) = Result.success(RegistrationResult(true))
        override suspend fun login(username: String, password: String) = Result.success(UserSession("user-1", username, "token", null))
        override suspend fun getAvailableUsers() = Result.success(emptyList<org.mulletaflix.domain.repository.AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.success(QuickConnectState("123456", "secret", false))
        override suspend fun checkQuickConnect(secret: String) = Result.success(null)
        override suspend fun logout() = Result.success(Unit)
        override suspend fun setServerUrl(url: String) = Unit
    }

    private class FakeSettingsRepository : SettingsRepository {
        override fun getTheme() = MutableStateFlow(AppThemeSetting.Dark)
        override suspend fun setTheme(theme: AppThemeSetting) = Unit
        override fun isPiPEnabled() = MutableStateFlow(true)
        override suspend fun setPiPEnabled(enabled: Boolean) = Unit
        override fun getPreferredAudioLanguage() = MutableStateFlow<String?>(null)
        override suspend fun setPreferredAudioLanguage(language: String?) = Unit
        override fun getPreferredSubtitleLanguage() = MutableStateFlow<String?>(null)
        override suspend fun setPreferredSubtitleLanguage(language: String?) = Unit
        override fun isAutoPlayEnabled() = MutableStateFlow(true)
        override suspend fun setAutoPlayEnabled(enabled: Boolean) = Unit
        override fun isSkipIntroEnabled() = MutableStateFlow(true)
        override suspend fun setSkipIntroEnabled(enabled: Boolean) = Unit
        override fun getDefaultQuality() = MutableStateFlow("Auto")
        override suspend fun setDefaultQuality(quality: String) = Unit
        override fun getDefaultPlaybackSpeed() = MutableStateFlow(1f)
        override suspend fun setDefaultPlaybackSpeed(speed: Float) = Unit
        override suspend fun clearLocalPreferences() = Unit
        override fun getSubtitleFontSize() = MutableStateFlow(100)
        override suspend fun setSubtitleFontSize(size: Int) = Unit
        override fun getDefaultAspectRatio() = MutableStateFlow("FIT")
        override suspend fun setDefaultAspectRatio(aspectRatio: String) = Unit
        override fun isLibraryGridViewEnabled() = MutableStateFlow(true)
        override suspend fun setLibraryGridViewEnabled(enabled: Boolean) = Unit
        override fun getLibraryGridDensity() = MutableStateFlow(LIBRARY_GRID_DENSITY_COMFORTABLE)
        override suspend fun setLibraryGridDensity(density: String) = Unit
        override fun getDefaultLibrarySort() = MutableStateFlow("SortName")
        override suspend fun setDefaultLibrarySort(sortBy: String) = Unit
        override fun getDefaultLibrarySortOrder() = MutableStateFlow("Ascending")
        override suspend fun setDefaultLibrarySortOrder(sortOrder: String) = Unit
        override fun getDefaultLibraryFilters() = MutableStateFlow(emptySet<String>())
        override suspend fun setDefaultLibraryFilters(filters: Set<String>) = Unit
    }
}
