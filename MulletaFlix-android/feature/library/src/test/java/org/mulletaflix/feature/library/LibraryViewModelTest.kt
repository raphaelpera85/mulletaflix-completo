package org.mulletaflix.feature.library

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
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
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.SettingsRepository

import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetLibraryItemsUseCase
import org.mulletaflix.core.common.network.NetworkMonitor

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class LibraryViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var media: FakeMediaRepository

    @Before fun setUp() {
        Dispatchers.setMain(dispatcher)
        media = FakeMediaRepository()
    }

    @After fun tearDown() = Dispatchers.resetMain()

    private fun createViewModel(auth: AuthRepository = FakeAuthRepository()): LibraryViewModel {
        return LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = auth,
            settingsRepository = FakeSettingsRepository(),
            networkMonitor = FakeNetworkMonitor(),
        )
    }

    @Test
    fun `library view mode is restored from and saved to local settings`() = runTest {
        val settings = FakeSettingsRepository(initialGridView = false)
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = settings,
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        assertEquals(false, viewModel.state.value.isGridView)
        viewModel.toggleView()
        advanceUntilIdle()
        assertEquals(true, viewModel.state.value.isGridView)
        assertEquals(true, settings.gridView)
    }

    @Test
    fun `library sort is restored from and saved to local settings`() = runTest {
        val settings = FakeSettingsRepository(initialSort = "DateCreated")
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = settings,
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        assertEquals(SortOption.DateAdded, viewModel.state.value.sortBy)
        viewModel.setSortBy(SortOption.CommunityRating)
        advanceUntilIdle()
        assertEquals(SortOption.CommunityRating, viewModel.state.value.sortBy)
        assertEquals("CommunityRating", settings.sort)
    }

    @Test
    fun `library sort order is restored saved and sent to the server`() = runTest {
        val settings = FakeSettingsRepository(initialSortOrder = "Descending")
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = settings,
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        assertEquals(SortOrder.Descending, viewModel.state.value.sortOrder)
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        assertEquals("Descending", media.lastSortOrder)

        viewModel.setSortOrder(SortOrder.Ascending)
        advanceUntilIdle()
        assertEquals(SortOrder.Ascending, viewModel.state.value.sortOrder)
        assertEquals("Ascending", settings.sortOrder)
        assertEquals("Ascending", media.lastSortOrder)
    }

    @Test
    fun `combined sort selection sends one query with field and direction`() = runTest {
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(),
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        val callsBefore = media.itemCalls

        viewModel.setSort(SortOption.ReleaseDate, SortOrder.Descending)
        advanceUntilIdle()

        assertEquals(callsBefore + 1, media.itemCalls)
        assertEquals("PremiereDate", media.lastSort)
        assertEquals("Descending", media.lastSortOrder)
    }

    @Test
    fun `first library request waits for persisted descending order`() = runTest {
        val settings = FakeSettingsRepository(initialSortOrder = "Descending")
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = settings,
            networkMonitor = FakeNetworkMonitor(),
        )

        // Intentionally load immediately, before init collectors have had a
        // chance to emit their saved values.
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals("Descending", media.lastSortOrder)
        assertEquals(SortOrder.Descending, viewModel.state.value.sortOrder)
    }

    @Test
    fun `refreshIfIdle does not cancel an active library request`() = runTest {
        val responseRelease = CompletableDeferred<Unit>()
        media.blockLibraryId = "library-1"
        media.blockedLibraryRelease = responseRelease
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(),
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        runCurrent()
        viewModel.refreshIfIdle("library-1")

        assertEquals(1, media.detailCalls)
        responseRelease.complete(Unit)
        advanceUntilIdle()
    }

    @Test
    fun `network recovery refreshes the loaded library once`() = runTest {
        val network = FakeNetworkMonitor(initialOnline = false)
        media.itemsByLibrary["library-1"] = listOf(MediaItem("item-1", "Item", MediaItemType.Movie)) to 2
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(),
            networkMonitor = network,
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        val callsBeforeRecovery = media.detailCalls

        network.setOnline(true)
        advanceUntilIdle()

        assertEquals(callsBeforeRecovery + 1, media.detailCalls)
        assertEquals(false, viewModel.state.value.isOffline)
    }

    @Test
    fun `refreshIfIdle does not poll the library while offline`() = runTest {
        val network = FakeNetworkMonitor(initialOnline = false)
        media.itemsByLibrary["library-1"] = emptyList<MediaItem>() to 0
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(),
            networkMonitor = network,
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        val callsWhileOffline = media.detailCalls
        val itemCallsWhileOffline = media.itemCalls
        assertEquals(-1, media.lastStartIndex)

        viewModel.refreshIfIdle("library-1")
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(callsWhileOffline, media.detailCalls)
        assertEquals(itemCallsWhileOffline, media.itemCalls)
        assertEquals(-1, media.lastStartIndex)
        assertTrue(viewModel.state.value.isOffline)
    }

    @Test
    fun `library filters are restored from and saved to local settings`() = runTest {
        val settings = FakeSettingsRepository(
            initialFilters = setOf(LibraryViewModel.FILTER_PLAYED, LibraryViewModel.FILTER_FAVORITES, "desconhecido"),
        )
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = settings,
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        assertEquals(
            listOf(LibraryViewModel.FILTER_FAVORITES, LibraryViewModel.FILTER_PLAYED),
            viewModel.state.value.activeFilters,
        )
        viewModel.toggleFilter(LibraryViewModel.FILTER_UNPLAYED)
        advanceUntilIdle()
        assertEquals(
            setOf(LibraryViewModel.FILTER_FAVORITES, LibraryViewModel.FILTER_UNPLAYED),
            settings.filters,
        )
        viewModel.clearFilters()
        advanceUntilIdle()
        assertEquals(emptySet<String>(), settings.filters)
    }

    @Test
    fun `pagination failure keeps current page and exposes retryable error`() = runTest {
        val first = MediaItem("first", "First", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        val viewModel = createViewModel()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        assertEquals(listOf(first), viewModel.state.value.items)

        // The next page starts at the number of items already loaded (1).
        media.pages[1] = Result.failure(IllegalStateException("network"))
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(listOf(first), viewModel.state.value.items)
        assertTrue(viewModel.state.value.error?.contains("network") == true)
        assertTrue(viewModel.state.value.hasMore)
    }

    @Test
    fun `library filters are sent to the server`() = runTest {
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = createViewModel()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        viewModel.toggleFilter(LibraryViewModel.FILTER_FAVORITES)
        advanceUntilIdle()

        assertEquals(true, media.lastIsFavorite)
        assertEquals(null, media.lastIsPlayed)

        viewModel.toggleFilter(LibraryViewModel.FILTER_PLAYED)
        advanceUntilIdle()
        assertEquals(true, media.lastIsFavorite)
        assertEquals(true, media.lastIsPlayed)
    }

    @Test
    fun `library browse request is restricted to the library item types`() = runTest {
        media.libraryCollectionType = "tvshows"
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = createViewModel()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals("Series", media.lastIncludeItemTypes)
    }

    @Test
    fun `loadMore pages from the items actually loaded`() = runTest {
        val first = MediaItem("first", "First", MediaItemType.Series)
        media.pages[0] = Result.success(listOf(first) to 10)
        val viewModel = createViewModel()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        media.pages[1] = Result.success(emptyList<MediaItem>() to 10)
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(1, media.lastStartIndex)
    }

    @Test
    fun `pagination clears loading state when the session has expired`() = runTest {
        val first = MediaItem("first", "First", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        val auth = FakeAuthRepository()
        val viewModel = createViewModel(auth = auth)

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        auth.userIdState.value = null
        advanceUntilIdle()
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(false, viewModel.state.value.isLoading)
        assertTrue(viewModel.state.value.error?.contains("Sessão expirada") == true)
    }

    @Test
    fun `stale library response cannot replace the latest library`() = runTest {
        val oldItems = listOf(MediaItem("old-item", "Biblioteca antiga", MediaItemType.Movie))
        val newItems = listOf(MediaItem("new-item", "Biblioteca atual", MediaItemType.Movie))
        media.itemsByLibrary["old-library"] = oldItems to 1
        media.itemsByLibrary["new-library"] = newItems to 1
        media.blockLibraryId = "old-library"
        val viewModel = createViewModel()

        viewModel.loadLibrary("old-library")
        runCurrent()
        viewModel.loadLibrary("new-library")
        advanceUntilIdle()

        assertEquals(listOf(newItems), listOf(viewModel.state.value.items))
        media.releaseBlockedLibrary()
        advanceUntilIdle()

        assertEquals(newItems, viewModel.state.value.items)
        assertEquals("Biblioteca atual", viewModel.state.value.libraryName)
    }

    @Test
    fun `late library response from a previous user cannot replace current session`() = runTest {
        val auth = FakeAuthRepository()
        media.blockLibraryId = "library-1"
        media.itemsByLibrary["library-1"] =
            listOf(MediaItem("old-item", "Conta antiga", MediaItemType.Movie)) to 1
        val viewModel = createViewModel(auth)

        viewModel.loadLibrary("library-1")
        runCurrent()

        auth.userIdState.value = "user-2"
        runCurrent()
        media.releaseBlockedLibrary()
        advanceUntilIdle()

        assertTrue(viewModel.state.value.items.isEmpty())
        assertEquals(false, viewModel.state.value.hasMore)
        assertEquals(false, viewModel.state.value.isLoading)
    }

    @Test
    fun `empty page stops pagination instead of spinning the sentinel`() = runTest {
        val first = MediaItem("first", "First", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 10)
        val viewModel = createViewModel()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        assertTrue("expected a second page to be pending", viewModel.state.value.hasMore)

        // The server reports a larger total but hands back nothing.
        media.pages[1] = Result.success(emptyList<MediaItem>() to 10)
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(
            "an empty page against a stale total must end pagination",
            false,
            viewModel.state.value.hasMore,
        )
        assertEquals(listOf(first), viewModel.state.value.items)
    }

    @Test
    fun `switching library drops the previous catalog before the first page fails`() = runTest {
        // If the previous library's items survive, a failed first page for the
        // new library leaves hasMore true over them and the next page is
        // requested at an offset that skips the new library's first items.
        val oldItems = List(40) { MediaItem("old-$it", "Antigo $it", MediaItemType.Movie) }
        media.itemsByLibrary["old-library"] = oldItems to 100
        media.itemsByLibrary["new-library"] = emptyList<MediaItem>() to 100
        val viewModel = createViewModel()

        viewModel.loadLibrary("old-library")
        advanceUntilIdle()
        assertEquals(40, viewModel.state.value.items.size)
        assertTrue(viewModel.state.value.hasMore)

        // Point the new library at a failure by removing its entry and using
        // the page map, then load it.
        media.itemsByLibrary.remove("new-library")
        media.pages.clear()
        media.pages[0] = Result.failure(IllegalStateException("falha simulada"))
        viewModel.loadLibrary("new-library")
        advanceUntilIdle()

        assertTrue(
            "the previous library's catalog must not survive the switch",
            viewModel.state.value.items.isEmpty(),
        )
        assertEquals(
            "a failed first page must not leave pagination pending",
            false,
            viewModel.state.value.hasMore,
        )
    }

    private class FakeMediaRepository : MediaRepository {
        val pages = mutableMapOf<Int, Result<Pair<List<MediaItem>, Int>>>()
        val itemsByLibrary = mutableMapOf<String, Pair<List<MediaItem>, Int>>()
        var blockLibraryId: String? = null
        var lastIsPlayed: Boolean? = null
        var lastIsFavorite: Boolean? = null
        var lastIncludeItemTypes: String? = null
        var lastSort: String? = null
        var lastSortOrder: String? = null
        var lastStartIndex: Int = -1
        var libraryCollectionType: String? = null
        var detailCalls: Int = 0
        var itemCalls: Int = 0
        var blockedLibraryRelease = CompletableDeferred<Unit>()
        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> =
            (itemsByLibrary[parentId]?.let { Result.success(it) } ?: pages[startIndex]).also {
                itemCalls++
                lastIsPlayed = isPlayed
                lastIsFavorite = isFavorite
                lastIncludeItemTypes = includeItemTypes
                lastSort = sortBy
                lastSortOrder = sortOrder
                lastStartIndex = startIndex
            } ?: Result.success(emptyList<MediaItem>() to 0)
        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> {
            detailCalls++
            if (itemId == blockLibraryId) {
                withContext(NonCancellable) { blockedLibraryRelease.await() }
            }
            val name = if (itemId == "new-library") "Biblioteca atual" else if (itemId == "old-library") "Biblioteca antiga" else "Biblioteca"
            return Result.success(MediaItem(itemId, name, MediaItemType.CollectionFolder, collectionType = libraryCollectionType))
        }
        fun releaseBlockedLibrary() {
            if (!blockedLibraryRelease.isCompleted) blockedLibraryRelease.complete(Unit)
        }
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
        override suspend fun search(userId: String, searchTerm: String, limit: Int, includeItemTypes: String?) = Result.success(emptyList<MediaItem>())
        override suspend fun getLiveTvChannels(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getRecordings(userId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getSuggestions(userId: String, itemId: String) = Result.success(emptyList<MediaItem>())
        override fun observeFavorites(userId: String): Flow<List<MediaItem>> = MutableStateFlow(emptyList())
        override fun observeRecentlyWatched(userId: String): Flow<List<MediaItem>> = MutableStateFlow(emptyList())
    }

    private class FakeNetworkMonitor(initialOnline: Boolean = true) : NetworkMonitor {
        private val online = MutableStateFlow(initialOnline)
        override val isOnline: Flow<Boolean> = online

        fun setOnline(value: Boolean) {
            online.value = value
        }
    }

    private class FakeSettingsRepository(
        initialGridView: Boolean = true,
        initialSort: String = "SortName",
        initialSortOrder: String = "Ascending",
        initialFilters: Set<String> = emptySet(),
    ) : SettingsRepository {
        var gridView = initialGridView
        var sort = initialSort
        var sortOrder = initialSortOrder
        var filters = initialFilters
        override fun getTheme() = MutableStateFlow(AppThemeSetting.Dark)
        override suspend fun setTheme(theme: AppThemeSetting) = Unit
        override fun getMaxBitrate() = MutableStateFlow(0)
        override suspend fun setMaxBitrate(bitrate: Int) = Unit
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
        override fun isLibraryGridViewEnabled() = MutableStateFlow(gridView)
        override suspend fun setLibraryGridViewEnabled(enabled: Boolean) { gridView = enabled }
        override fun getDefaultLibrarySort() = MutableStateFlow(sort)
        override suspend fun setDefaultLibrarySort(sortBy: String) { sort = sortBy }
        override fun getDefaultLibrarySortOrder() = MutableStateFlow(sortOrder)
        override suspend fun setDefaultLibrarySortOrder(sortOrder: String) { this.sortOrder = sortOrder }
        override fun getDefaultLibraryFilters() = MutableStateFlow(filters)
        override suspend fun setDefaultLibraryFilters(filters: Set<String>) { this.filters = filters }
    }

    private class FakeAuthRepository(
        private val userId: String? = "user-1",
    ) : AuthRepository {
        val userIdState = MutableStateFlow(userId)
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
}
