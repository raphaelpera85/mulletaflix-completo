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
        )
        advanceUntilIdle()

        assertEquals(SortOption.DateAdded, viewModel.state.value.sortBy)
        viewModel.setSortBy(SortOption.CommunityRating)
        advanceUntilIdle()
        assertEquals(SortOption.CommunityRating, viewModel.state.value.sortBy)
        assertEquals("CommunityRating", settings.sort)
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

    private class FakeMediaRepository : MediaRepository {
        val pages = mutableMapOf<Int, Result<Pair<List<MediaItem>, Int>>>()
        val itemsByLibrary = mutableMapOf<String, Pair<List<MediaItem>, Int>>()
        var blockLibraryId: String? = null
        private var blockedLibraryRelease = CompletableDeferred<Unit>()
        var lastIsPlayed: Boolean? = null
        var lastIsFavorite: Boolean? = null
        var lastIncludeItemTypes: String? = null
        var lastStartIndex: Int = -1
        var libraryCollectionType: String? = null
        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> =
            (itemsByLibrary[parentId]?.let { Result.success(it) } ?: pages[startIndex]).also {
                lastIsPlayed = isPlayed
                lastIsFavorite = isFavorite
                lastIncludeItemTypes = includeItemTypes
                lastStartIndex = startIndex
            } ?: Result.success(emptyList<MediaItem>() to 0)
        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> {
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

    private class FakeSettingsRepository(
        initialGridView: Boolean = true,
        initialSort: String = "SortName",
        initialFilters: Set<String> = emptySet(),
    ) : SettingsRepository {
        var gridView = initialGridView
        var sort = initialSort
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
