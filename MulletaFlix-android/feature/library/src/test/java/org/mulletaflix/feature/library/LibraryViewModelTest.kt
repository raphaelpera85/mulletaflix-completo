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
import org.mulletaflix.domain.model.LibraryFilterOptions
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.repository.LibraryCatalogCache
import org.mulletaflix.domain.model.CachedLibraryCatalog

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

    private fun createViewModel(
        auth: AuthRepository = FakeAuthRepository(),
        settings: FakeSettingsRepository = FakeSettingsRepository(),
        network: FakeNetworkMonitor = FakeNetworkMonitor(),
        catalogCache: LibraryCatalogCache = org.mulletaflix.domain.repository.NoOpLibraryCatalogCache,
    ): LibraryViewModel {
        return LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = auth,
            settingsRepository = settings,
            networkMonitor = network,
            libraryCatalogCache = catalogCache,
        )
    }

    @Test
    fun `last confirmed library page is saved and restored offline without network calls`() = runTest {
        val first = MediaItem("movie-1", "Filme 1", MediaItemType.Movie, overview = "Sinopse segura")
        val second = MediaItem("movie-2", "Filme 2", MediaItemType.Movie)
        media.itemsByLibrary["library-1"] = listOf(first, second) to 2
        val cache = FakeLibraryCatalogCache()
        val online = FakeNetworkMonitor()
        val onlineVm = createViewModel(network = online, catalogCache = cache)
        advanceUntilIdle()
        onlineVm.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(listOf(first, second), cache.snapshots.getValue("user-1" to "library-1").items)

        val offlineMediaCalls = media.itemCalls
        val offlineDetailCalls = media.detailCalls
        val offlineVm = createViewModel(network = FakeNetworkMonitor(initialOnline = false), catalogCache = cache)
        advanceUntilIdle()
        offlineVm.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(listOf(first, second), offlineVm.state.value.items)
        assertEquals("Biblioteca", offlineVm.state.value.libraryName)
        assertTrue(offlineVm.state.value.isShowingCachedCatalog)
        assertTrue(offlineVm.state.value.catalogSavedAtEpochMillis != null)
        assertEquals(false, offlineVm.state.value.hasMore)
        assertEquals(2, offlineVm.state.value.catalogTotalItemCount)
        assertEquals(offlineMediaCalls, media.itemCalls)
        assertEquals(offlineDetailCalls, media.detailCalls)
    }

    @Test
    fun `network loss during library request restores matching saved snapshot`() = runTest {
        val saved = MediaItem("saved-movie", "Salvo", MediaItemType.Movie)
        val cache = FakeLibraryCatalogCache().apply {
            snapshots["user-1" to "library-1"] = CachedLibraryCatalog(
                libraryId = "library-1",
                libraryName = "Filmes",
                collectionType = "movies",
                items = listOf(saved),
                sortBy = "SortName",
                sortOrder = "Ascending",
                activeFilters = emptyList(),
                savedAtEpochMillis = 1234L,
                totalItemCount = 1,
            )
        }
        val network = FakeNetworkMonitor()
        val pendingResponse = CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>()
        media.responseSequence = ArrayDeque(listOf(pendingResponse))
        val viewModel = createViewModel(network = network, catalogCache = cache)
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        runCurrent()
        assertEquals(1, media.itemCalls)

        network.setOnline(false)
        runCurrent()
        pendingResponse.complete(Result.failure(IllegalStateException("A conexão caiu")))
        advanceUntilIdle()

        assertEquals(listOf(saved), viewModel.state.value.items)
        assertTrue(viewModel.state.value.isShowingCachedCatalog)
        assertEquals("Filmes", viewModel.state.value.libraryName)
        assertEquals(null, viewModel.state.value.error)
    }

    @Test
    fun `network loss without snapshot preserves results already loaded in memory`() = runTest {
        val loaded = MediaItem("movie-1", "Já carregado", MediaItemType.Movie)
        media.itemsByLibrary["library-1"] = listOf(loaded) to 1
        val cache = FakeLibraryCatalogCache()
        val network = FakeNetworkMonitor()
        val viewModel = createViewModel(network = network, catalogCache = cache)
        advanceUntilIdle()
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        cache.snapshots.clear()

        val pendingResponse = CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>()
        media.responseSequence = ArrayDeque(listOf(pendingResponse))
        viewModel.loadLibrary("library-1")
        runCurrent()
        network.setOnline(false)
        runCurrent()
        pendingResponse.complete(Result.failure(IllegalStateException("A conexão caiu")))
        advanceUntilIdle()

        assertEquals(listOf(loaded), viewModel.state.value.items)
        assertEquals(false, viewModel.state.value.isShowingCachedCatalog)
        assertTrue(viewModel.state.value.error.orEmpty().contains("resultados carregados anteriormente"))
    }

    @Test
    fun `network return during cache read refreshes once after restoring snapshot`() = runTest {
        val saved = MediaItem("saved-movie", "Salvo", MediaItemType.Movie)
        val snapshot = CachedLibraryCatalog(
            libraryId = "library-1",
            libraryName = "Filmes",
            collectionType = "movies",
            items = listOf(saved),
            sortBy = "SortName",
            sortOrder = "Ascending",
            activeFilters = emptyList(),
            savedAtEpochMillis = 1234L,
            totalItemCount = 1,
        )
        val cache = FakeLibraryCatalogCache()
        val pendingRead = CompletableDeferred<CachedLibraryCatalog?>()
        cache.pendingRead = pendingRead
        val pendingOnlineResponse = CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>()
        media.responseSequence = ArrayDeque(listOf(pendingOnlineResponse))
        val network = FakeNetworkMonitor(initialOnline = false)
        val viewModel = createViewModel(network = network, catalogCache = cache)
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        runCurrent()
        assertEquals(0, media.itemCalls)

        network.setOnline(true)
        runCurrent()
        assertEquals(0, media.itemCalls)
        pendingRead.complete(snapshot)
        runCurrent()

        assertEquals(1, media.itemCalls)
        assertEquals(1, media.detailCalls)
        assertEquals(listOf(saved), viewModel.state.value.items)
        assertEquals(true, viewModel.state.value.isShowingCachedCatalog)

        pendingOnlineResponse.complete(Result.success(emptyList<MediaItem>() to 0))
        advanceUntilIdle()
        assertEquals(false, viewModel.state.value.isOffline)
        assertEquals(false, viewModel.state.value.isShowingCachedCatalog)
    }

    @Test
    fun `offline library snapshot cannot cross library or account boundary`() = runTest {
        val cache = FakeLibraryCatalogCache().apply {
            snapshots["user-1" to "library-1"] = CachedLibraryCatalog(
                libraryId = "library-1",
                libraryName = "Minha Biblioteca",
                collectionType = "movies",
                items = listOf(MediaItem("movie-1", "Privado", MediaItemType.Movie)),
                sortBy = "SortName",
                sortOrder = "Ascending",
                activeFilters = emptyList(),
                savedAtEpochMillis = 1234L,
                totalItemCount = 1,
            )
        }
        val viewModel = createViewModel(
            auth = FakeAuthRepository("user-2"),
            network = FakeNetworkMonitor(initialOnline = false),
            catalogCache = cache,
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(emptyList<MediaItem>(), viewModel.state.value.items)
        assertEquals(false, viewModel.state.value.isShowingCachedCatalog)
        assertTrue(viewModel.state.value.error.orEmpty().contains("Nenhuma lista salva"))
        assertEquals("user-2" to "library-1", cache.lastReadKey)
    }

    @Test
    fun `offline tv never restores a cached books library`() = runTest {
        val cache = FakeLibraryCatalogCache().apply {
            snapshots["user-1" to "books-library"] = CachedLibraryCatalog(
                libraryId = "books-library",
                libraryName = "Livros",
                collectionType = "books",
                items = listOf(MediaItem("book-1", "Livro", MediaItemType.Book)),
                sortBy = "SortName",
                sortOrder = "Ascending",
                activeFilters = emptyList(),
                savedAtEpochMillis = 1234L,
                totalItemCount = 1,
            )
        }
        val viewModel = createViewModel(network = FakeNetworkMonitor(initialOnline = false), catalogCache = cache)
        advanceUntilIdle()

        viewModel.loadLibrary("books-library", isTelevision = true)
        advanceUntilIdle()

        assertEquals(emptyList<MediaItem>(), viewModel.state.value.items)
        assertEquals(false, viewModel.state.value.isShowingCachedCatalog)
        assertEquals("A biblioteca de Livros não está disponível na Android TV.", viewModel.state.value.error)
    }

    @Test
    fun `offline sort and filters cannot relabel or alter cached query`() = runTest {
        val cache = FakeLibraryCatalogCache().apply {
            snapshots["user-1" to "library-1"] = CachedLibraryCatalog(
                libraryId = "library-1",
                libraryName = "Biblioteca",
                collectionType = "movies",
                items = listOf(MediaItem("movie-1", "Filme", MediaItemType.Movie)),
                sortBy = "PremiereDate",
                sortOrder = "Descending",
                activeFilters = listOf(LibraryViewModel.FILTER_FAVORITES),
                savedAtEpochMillis = 1234L,
                totalItemCount = 1,
            )
        }
        val viewModel = createViewModel(network = FakeNetworkMonitor(initialOnline = false), catalogCache = cache)
        advanceUntilIdle()
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        viewModel.setSort(SortOption.Name, SortOrder.Ascending)
        viewModel.toggleFilter(LibraryViewModel.FILTER_PLAYED)
        viewModel.clearFilters()
        advanceUntilIdle()

        assertEquals(SortOption.ReleaseDate, viewModel.state.value.sortBy)
        assertEquals(SortOrder.Descending, viewModel.state.value.sortOrder)
        assertEquals(listOf(LibraryViewModel.FILTER_FAVORITES), viewModel.state.value.activeFilters)
        assertEquals(0, cache.writeCount)
        assertEquals(0, media.itemCalls)
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
    fun `direct tv navigation to a books library does not request or show its items`() = runTest {
        media.libraryCollectionType = " books "
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadLibrary("books-library", isTelevision = true)
        advanceUntilIdle()

        assertEquals(0, media.itemCalls)
        assertEquals(emptyList<MediaItem>(), viewModel.state.value.items)
        assertEquals(false, viewModel.state.value.isLoading)
        assertEquals("A biblioteca de Livros não está disponível na Android TV.", viewModel.state.value.error)
    }

    @Test
    fun `tv library query excludes book types when collection metadata is unknown`() = runTest {
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadLibrary("unknown-library", isTelevision = true)
        advanceUntilIdle()

        assertTrue(media.lastIncludeItemTypes.orEmpty().split(',').none { it in setOf("Book", "Audiobook") })
        assertTrue(media.lastIncludeItemTypes.orEmpty().contains("Movie"))
    }

    @Test
    fun `tv library keeps book types excluded after filter and sort reloads`() = runTest {
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = createViewModel()
        advanceUntilIdle()

        viewModel.loadLibrary("unknown-library", isTelevision = true)
        advanceUntilIdle()
        assertTrue(media.lastIncludeItemTypes.orEmpty().split(',').none { it in setOf("Book", "Audiobook") })

        viewModel.setSortOrder(SortOrder.Descending)
        advanceUntilIdle()
        assertTrue(media.lastIncludeItemTypes.orEmpty().split(',').none { it in setOf("Book", "Audiobook") })

        viewModel.toggleFilter(LibraryViewModel.FILTER_FAVORITES)
        advanceUntilIdle()
        assertTrue(media.lastIncludeItemTypes.orEmpty().split(',').none { it in setOf("Book", "Audiobook") })

        viewModel.clearFilters()
        advanceUntilIdle()
        assertTrue(media.lastIncludeItemTypes.orEmpty().split(',').none { it in setOf("Book", "Audiobook") })

        viewModel.refreshIfIdle("unknown-library")
        advanceUntilIdle()
        assertTrue(media.lastIncludeItemTypes.orEmpty().split(',').none { it in setOf("Book", "Audiobook") })
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
    fun `refreshIfIdle does not replace the initial job before loading state is published`() = runTest {
        val responseRelease = CompletableDeferred<Unit>()
        media.blockLibraryId = "library-1"
        media.blockedLibraryRelease = responseRelease
        val viewModel = createViewModel()
        advanceUntilIdle()

        // The initial coroutine is active, but it has not reached the point
        // where it publishes isLoading yet. This is the TV-entry race window.
        viewModel.loadLibrary("library-1")
        viewModel.refreshIfIdle("library-1")
        runCurrent()

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
    fun `network recovery keeps book types excluded for a tv library`() = runTest {
        val network = FakeNetworkMonitor(initialOnline = false)
        media.pages[0] = Result.success(emptyList<MediaItem>() to 0)
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(),
            networkMonitor = network,
        )
        advanceUntilIdle()

        viewModel.loadLibrary("unknown-library", isTelevision = true)
        advanceUntilIdle()
        network.setOnline(true)
        advanceUntilIdle()

        assertTrue(media.lastIncludeItemTypes.orEmpty().split(',').none { it in setOf("Book", "Audiobook") })
        assertTrue(media.lastIncludeItemTypes.orEmpty().contains("Movie"))
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
    fun `available facet options load only when filter dialog opens and are cached until library reload`() = runTest {
        media.filterOptions = LibraryFilterOptions(
            genres = listOf("Drama", "Ação"),
            years = listOf(2024),
            officialRatings = listOf("PG-13"),
        )
        val viewModel = createViewModel()
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(null, viewModel.state.value.availableFilterOptions)
        assertEquals(0, media.filterOptionsCalls)

        viewModel.showFilterMenu()
        advanceUntilIdle()

        assertEquals(media.filterOptions, viewModel.state.value.availableFilterOptions)
        assertEquals(1, media.filterOptionsCalls)
        assertEquals("library-1", media.lastFilterParentId)
        assertEquals(media.lastIncludeItemTypes, media.lastFilterIncludeItemTypes)

        viewModel.hideFilterMenu()
        viewModel.showFilterMenu()
        advanceUntilIdle()
        assertEquals("reopening the same library filter reuses its options", 1, media.filterOptionsCalls)
    }

    @Test
    fun `late filter options from the previous library are discarded`() = runTest {
        val pending = CompletableDeferred<Result<LibraryFilterOptions>>()
        media.pendingFilterOptions = pending
        val viewModel = createViewModel()
        viewModel.loadLibrary("old-library")
        advanceUntilIdle()

        viewModel.showFilterMenu()
        runCurrent()
        assertEquals(true, viewModel.state.value.isLoadingFilterOptions)

        media.pendingFilterOptions = null
        viewModel.loadLibrary("new-library")
        advanceUntilIdle()
        pending.complete(Result.success(LibraryFilterOptions(genres = listOf("Old genre"))))
        advanceUntilIdle()

        assertEquals(null, viewModel.state.value.availableFilterOptions)
        assertEquals(false, viewModel.state.value.isLoadingFilterOptions)
    }

    @Test
    fun `library facets combine with status filters persist and survive pagination`() = runTest {
        val first = MediaItem("first", "First", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(first) to 2)
        media.pages[1] = Result.success(listOf(MediaItem("second", "Second", MediaItemType.Movie)) to 2)
        val settings = FakeSettingsRepository()
        val viewModel = createViewModel(settings = settings)
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertTrue(
            viewModel.applyFilters(
                basicFilters = setOf(LibraryViewModel.FILTER_FAVORITES, LibraryViewModel.FILTER_PLAYED),
                facets = LibraryFacetFilters(
                    genres = " Drama, Ação, Drama ",
                    years = "2023, 2024",
                    officialRatings = "PG-13, TV-MA",
                ),
            ),
        )
        advanceUntilIdle()

        assertEquals("Drama|Ação", media.lastGenres)
        assertEquals("2023,2024", media.lastYears)
        assertEquals("PG-13|TV-MA", media.lastOfficialRatings)
        assertEquals(true, media.lastIsPlayed)
        assertEquals(true, media.lastIsFavorite)
        assertTrue("library_genres:Drama|Ação" in settings.filters)
        assertTrue("library_years:2023,2024" in settings.filters)
        assertTrue("library_ratings:PG-13|TV-MA" in settings.filters)

        viewModel.loadMore()
        advanceUntilIdle()
        assertEquals("Drama|Ação", media.lastGenres)
        assertEquals("2023,2024", media.lastYears)
        assertEquals("PG-13|TV-MA", media.lastOfficialRatings)
        assertEquals(true, media.lastIsPlayed)
        assertEquals(true, media.lastIsFavorite)

        viewModel.removeFilter("library_genres:Drama|Ação")
        advanceUntilIdle()
        assertEquals(null, media.lastGenres)
        assertEquals("2023,2024", media.lastYears)
        assertTrue("library_genres:Drama|Ação" !in settings.filters)
    }

    @Test
    fun `invalid year selection does not replace active filters`() = runTest {
        val viewModel = createViewModel()
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        viewModel.toggleFilter(LibraryViewModel.FILTER_FAVORITES)
        advanceUntilIdle()
        val activeBefore = viewModel.state.value.activeFilters

        assertEquals(false, viewModel.applyFilters(emptySet(), LibraryFacetFilters(years = "20xx")))
        assertEquals(activeBefore, viewModel.state.value.activeFilters)
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

    /**
     * "Aleatório" is `ORDER BY RANDOM()` on the server, so a request at an offset
     * cuts a different shuffle: the accumulated list gains repeats and loses titles
     * that were never shown, and the "loaded < total" check ends the catalogue early.
     * The whole list therefore has to arrive in one request.
     */
    @Test
    fun `a random ordering is fetched whole instead of paged by offset`() = runTest {
        val whole = List(120) { MediaItem("item-$it", "Item $it", MediaItemType.Movie) }
        media.responseSequence = ArrayDeque(
            listOf(
                CompletableDeferred(Result.success(listOf(whole.first()) to 120)),
                CompletableDeferred(Result.success(whole to 120)),
            ),
        )
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(initialSort = "Random"),
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals("the second request must ask for the whole library", 120, media.lastLimit)
        assertEquals(120, viewModel.state.value.items.size)
        assertEquals(false, viewModel.state.value.hasMore)

        val callsAfterLoad = media.itemCalls
        viewModel.loadMore()
        advanceUntilIdle()
        assertEquals("there is no page 2 to ask for", callsAfterLoad, media.itemCalls)
    }

    @Test
    fun `a failed full random catalog request is reported and can be retried`() = runTest {
        val whole = List(120) { MediaItem("item-$it", "Item $it", MediaItemType.Movie) }
        media.responseSequence = ArrayDeque(
            listOf(
                CompletableDeferred(Result.success(listOf(whole.first()) to 120)),
                CompletableDeferred(Result.failure(IllegalStateException("Falha na consulta completa"))),
                CompletableDeferred(Result.success(listOf(whole.first()) to 120)),
                CompletableDeferred(Result.success(whole to 120)),
            ),
        )
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(initialSort = "Random"),
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(emptyList<MediaItem>(), viewModel.state.value.items)
        assertEquals("Falha na consulta completa", viewModel.state.value.error)
        assertEquals(false, viewModel.state.value.isLoading)
        assertEquals(false, viewModel.state.value.isRefreshing)
        assertEquals(false, viewModel.state.value.hasMore)
        assertEquals(2, media.itemCalls)

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(whole, viewModel.state.value.items)
        assertEquals(null, viewModel.state.value.error)
        assertEquals(false, viewModel.state.value.isLoading)
        assertEquals(false, viewModel.state.value.isRefreshing)
        assertEquals(4, media.itemCalls)
    }

    @Test
    fun `failed random catalog refresh keeps previously loaded items`() = runTest {
        val whole = List(120) { MediaItem("item-$it", "Item $it", MediaItemType.Movie) }
        media.responseSequence = ArrayDeque(
            listOf(
                CompletableDeferred(Result.success(listOf(whole.first()) to 120)),
                CompletableDeferred(Result.success(whole to 120)),
                CompletableDeferred(Result.success(listOf(whole.first()) to 120)),
                CompletableDeferred(Result.failure(IllegalStateException("Falha na atualização completa"))),
            ),
        )
        val viewModel = LibraryViewModel(
            getLibraryItemsUseCase = GetLibraryItemsUseCase(media),
            getItemDetailUseCase = GetItemDetailUseCase(media),
            authRepository = FakeAuthRepository(),
            settingsRepository = FakeSettingsRepository(initialSort = "Random"),
            networkMonitor = FakeNetworkMonitor(),
        )
        advanceUntilIdle()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()
        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(whole, viewModel.state.value.items)
        assertEquals("Falha na atualização completa", viewModel.state.value.error)
        assertEquals(false, viewModel.state.value.isLoading)
        assertEquals(false, viewModel.state.value.isRefreshing)
    }

    /**
     * Two requests against an offset window are only disjoint when nothing changed on
     * the server in between. A library scan that inserts a title at the top of a
     * "date added" ordering shifts the window, so the tail of page 1 comes back as the
     * head of page 2 — the grid renders `key = item.id`, so the repeat has to be
     * dropped, and the offset has to keep advancing past every item the server handed
     * over, duplicates included.
     */
    @Test
    fun `a shifted page does not repeat an item and the offset keeps advancing`() = runTest {
        val a = MediaItem("a", "A", MediaItemType.Movie)
        val b = MediaItem("b", "B", MediaItemType.Movie)
        val c = MediaItem("c", "C", MediaItemType.Movie)
        val d = MediaItem("d", "D", MediaItemType.Movie)
        media.pages[0] = Result.success(listOf(a, b, c) to 10)
        val viewModel = createViewModel()

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        media.pages[3] = Result.success(listOf(c, d) to 10)
        viewModel.loadMore()
        advanceUntilIdle()

        assertEquals(
            "the repeated item must not be rendered twice",
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

    @Test
    fun `a load started without a session does not leave the skeleton up`() = runTest {
        // The TV screen starts the first load as soon as it is composed, which can
        // beat the observer that publishes the saved user. `loadLibrary` raises the
        // loading flags and only then discovers there is no session; the branch that
        // reports "Sessão expirada" has to lower them, because no request is coming
        // that would.
        val auth = FakeAuthRepository()
        auth.userIdState.value = null
        val viewModel = createViewModel(auth = auth)

        viewModel.loadLibrary("library-1")
        advanceUntilIdle()

        assertEquals(false, viewModel.state.value.isLoading)
        assertEquals(false, viewModel.state.value.isRefreshing)
        assertEquals(LibraryViewModel.EXPIRED_SESSION_MESSAGE, viewModel.state.value.error)
    }

    private class FakeLibraryCatalogCache : LibraryCatalogCache {
        val snapshots = mutableMapOf<Pair<String, String>, CachedLibraryCatalog>()
        var lastReadKey: Pair<String, String>? = null
        var writeCount = 0
        var pendingRead: CompletableDeferred<CachedLibraryCatalog?>? = null

        override suspend fun read(userId: String, libraryId: String): CachedLibraryCatalog? {
            lastReadKey = userId to libraryId
            return pendingRead?.await() ?: snapshots[userId to libraryId]
        }

        override suspend fun write(
            userId: String,
            libraryId: String,
            libraryName: String,
            collectionType: String?,
            sortBy: String,
            sortOrder: String,
            activeFilters: List<String>,
            items: List<MediaItem>,
            totalItemCount: Int,
        ) {
            writeCount++
            snapshots[userId to libraryId] = CachedLibraryCatalog(
                libraryId = libraryId,
                libraryName = libraryName,
                collectionType = collectionType,
                items = items,
                sortBy = sortBy,
                sortOrder = sortOrder,
                activeFilters = activeFilters,
                savedAtEpochMillis = 1234L,
                totalItemCount = totalItemCount,
            )
        }
    }

    private class FakeMediaRepository : MediaRepository {
        val pages = mutableMapOf<Int, Result<Pair<List<MediaItem>, Int>>>()
        val itemsByLibrary = mutableMapOf<String, Pair<List<MediaItem>, Int>>()
        var blockLibraryId: String? = null
        var lastIsPlayed: Boolean? = null
        var lastIsFavorite: Boolean? = null
        var lastGenres: String? = null
        var lastYears: String? = null
        var lastOfficialRatings: String? = null
        var lastIncludeItemTypes: String? = null
        var lastSort: String? = null
        var lastSortOrder: String? = null
        var lastStartIndex: Int = -1
        var lastLimit: Int = -1
        var libraryCollectionType: String? = null
        var detailCalls: Int = 0
        var itemCalls: Int = 0
        var filterOptionsCalls: Int = 0
        var filterOptions = LibraryFilterOptions()
        var pendingFilterOptions: CompletableDeferred<Result<LibraryFilterOptions>>? = null
        var lastFilterParentId: String? = null
        var lastFilterIncludeItemTypes: String? = null
        var blockedLibraryRelease = CompletableDeferred<Unit>()
        var responseSequence: ArrayDeque<CompletableDeferred<Result<Pair<List<MediaItem>, Int>>>>? = null
        override suspend fun getItems(userId: String, parentId: String?, includeItemTypes: String?, sortBy: String?, sortOrder: String?, filters: String?, searchTerm: String?, startIndex: Int, limit: Int, genres: String?, years: String?, officialRatings: String?, isPlayed: Boolean?, isFavorite: Boolean?): Result<Pair<List<MediaItem>, Int>> {
            itemCalls++
            lastIsPlayed = isPlayed
            lastIsFavorite = isFavorite
            lastGenres = genres
            lastYears = years
            lastOfficialRatings = officialRatings
            lastIncludeItemTypes = includeItemTypes
            lastSort = sortBy
            lastSortOrder = sortOrder
            lastStartIndex = startIndex
            lastLimit = limit
            return responseSequence?.removeFirstOrNull()?.let { deferred ->
                withContext(NonCancellable) { deferred.await() }
            } ?: itemsByLibrary[parentId]?.let { Result.success(it) } ?: pages[startIndex] ?: Result.success(emptyList<MediaItem>() to 0)
        }
        override suspend fun getLibraryFilterOptions(userId: String, parentId: String, includeItemTypes: String?): Result<LibraryFilterOptions> {
            filterOptionsCalls++
            lastFilterParentId = parentId
            lastFilterIncludeItemTypes = includeItemTypes
            return pendingFilterOptions?.let { deferred ->
                withContext(NonCancellable) { deferred.await() }
            } ?: Result.success(filterOptions)
        }
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
        override suspend fun getLiveTvChannelPreview(userId: String) = Result.success(emptyList<MediaItem>())
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
