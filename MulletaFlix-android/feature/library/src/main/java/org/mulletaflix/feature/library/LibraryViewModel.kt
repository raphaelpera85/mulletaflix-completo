package org.mulletaflix.feature.library

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.mulletaflix.core.common.network.NetworkMonitor
import java.util.Locale
import org.mulletaflix.domain.model.LibraryBrowseTypes
import org.mulletaflix.domain.model.LibraryFilterOptions
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.paging.appendDistinctBy
import org.mulletaflix.domain.paging.hasMorePages
import org.mulletaflix.domain.paging.shouldRequestNextPage
import org.mulletaflix.domain.paging.singleRequestItemLimit
import org.mulletaflix.domain.paging.supportsOffsetPaging
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.LibraryCatalogCache
import org.mulletaflix.domain.repository.NoOpLibraryCatalogCache
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetLibraryItemsUseCase
import javax.inject.Inject

data class LibraryState(
    val libraryName: String = "Biblioteca",
    val isOffline: Boolean = false,
    val isGridView: Boolean = true,
    val gridDensity: String = LIBRARY_GRID_DENSITY_COMFORTABLE,
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val items: List<MediaItem> = emptyList(),
    val activeFilters: List<String> = emptyList(),
    val availableFilterOptions: LibraryFilterOptions? = null,
    val isLoadingFilterOptions: Boolean = false,
    val filterOptionsError: String? = null,
    val hasMore: Boolean = false,
    val isShowingCachedCatalog: Boolean = false,
    val catalogSavedAtEpochMillis: Long? = null,
    val catalogTotalItemCount: Int? = null,
    val letterNavigationTarget: String? = null,
    val error: String? = null,
    val showSortMenu: Boolean = false,
    val showFilterMenu: Boolean = false,
    val sortBy: SortOption = SortOption.Name,
    val sortOrder: SortOrder = SortOrder.Ascending,
)

@HiltViewModel
class LibraryViewModel @Inject constructor(
    private val getLibraryItemsUseCase: GetLibraryItemsUseCase,
    private val getItemDetailUseCase: GetItemDetailUseCase,
    private val authRepository: AuthRepository,
    private val settingsRepository: SettingsRepository,
    private val networkMonitor: NetworkMonitor,
    private val libraryCatalogCache: LibraryCatalogCache = NoOpLibraryCatalogCache,
) : ViewModel() {

    private val _state = MutableStateFlow(LibraryState())
    val state: StateFlow<LibraryState> = _state.asStateFlow()

    private var currentLibraryId: String? = null
    private var currentIsTelevision = false
    private var currentLibraryCollectionType: String? = null
    private var currentLibraryName: String = "Biblioteca"
    private var currentUserId: String? = null
    private var hasObservedUser = false
    private var currentIncludeItemTypes: String = LibraryBrowseTypes.DEFAULT
    private val pageSize = 40
    private var loadJob: Job? = null
    private var filterOptionsJob: Job? = null
    private var filterOptionsRequestGeneration = 0L

    /**
     * How many items the server has actually handed over, which is the offset the
     * next page starts at.
     *
     * It is not `items.size`: the list drops entries whose id is already present, so
     * after a shifted window the two numbers differ. Using the deduplicated size as
     * an offset would ask for a window that starts before the end of the previous
     * one, and a page made only of duplicates would leave the offset standing still.
     */
    private var fetchedItemCount = 0
    private var requestGeneration: Long = 0L
    private var sortPreferenceReady = false
    private var sortOrderPreferenceReady = false
    private var filtersPreferenceReady = false
    private val libraryQueryPreferencesReady = MutableStateFlow(false)

    companion object {
        const val FILTER_FAVORITES = "Favoritos"
        const val FILTER_PLAYED = "Assistidos"
        const val FILTER_UNPLAYED = "Não assistidos"
        val BASIC_FILTERS = listOf(FILTER_FAVORITES, FILTER_PLAYED, FILTER_UNPLAYED)

        /** Shown whenever a load is started without a usable session. */
        const val EXPIRED_SESSION_MESSAGE = "Sessão expirada. Entre novamente."
        private val SUPPORTED_FILTERS = BASIC_FILTERS
    }

    init {
        viewModelScope.launch {
            var previousOnline: Boolean? = null
            networkMonitor.isOnline.distinctUntilChanged().collect { online ->
                _state.update { it.copy(isOffline = !online) }
                if (!online) _state.update { it.copy(showSortMenu = false, showFilterMenu = false) }
                if (shouldRefreshLibraryOnNetworkReturn(previousOnline, online)) {
                    currentLibraryId?.let { libraryId ->
                        refreshIfIdle(libraryId, currentIsTelevision, deferUntilIdle = true)
                    }
                }
                previousOnline = online
            }
        }
        viewModelScope.launch {
            authRepository.getSavedUserId().collect { userId ->
                val userChanged = hasObservedUser && currentUserId != userId
                currentUserId = userId
                hasObservedUser = true
                if (userChanged) {
                    loadJob?.cancel()
                    ++requestGeneration
                    currentLibraryId = null
                    fetchedItemCount = 0
                    invalidateFilterOptions()
                    _state.update {
                        it.copy(
                            items = emptyList(),
                            letterNavigationTarget = null,
                            hasMore = false,
                            isShowingCachedCatalog = false,
                            catalogSavedAtEpochMillis = null,
                            catalogTotalItemCount = null,
                            isLoading = false,
                            isRefreshing = false,
                            showFilterMenu = false,
                            error = null,
                        )
                    }
                }
            }
        }
        viewModelScope.launch {
            settingsRepository.isLibraryGridViewEnabled().collect { enabled ->
                _state.update { it.copy(isGridView = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getLibraryGridDensity().collect { density ->
                _state.update { it.copy(gridDensity = normalizeLibraryGridDensity(density)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultLibrarySort().collect { sortValue ->
                val sort = SortOption.values().firstOrNull { it.apiValue.equals(sortValue, ignoreCase = true) }
                    ?: SortOption.Name
                _state.update { it.copy(sortBy = sort) }
                sortPreferenceReady = true
                updateLibraryQueryPreferencesReady()
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultLibrarySortOrder().collect { orderValue ->
                val order = SortOrder.values().firstOrNull { it.apiValue.equals(orderValue, ignoreCase = true) }
                    ?: SortOrder.Ascending
                _state.update { it.copy(sortOrder = order) }
                sortOrderPreferenceReady = true
                updateLibraryQueryPreferencesReady()
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultLibraryFilters().collect { filters ->
                _state.update { it.copy(activeFilters = orderedFilters(filters)) }
                filtersPreferenceReady = true
                updateLibraryQueryPreferencesReady()
            }
        }
    }

    fun loadLibrary(
        libraryId: String,
        isTelevision: Boolean = currentIsTelevision,
        preserveLetterNavigation: Boolean = false,
    ) {
        if (!preserveLetterNavigation) {
            _state.update { it.copy(letterNavigationTarget = null) }
        }
        loadJob?.cancel()
        invalidateFilterOptions()
        val requestGeneration = ++this.requestGeneration
        val switchedLibrary = currentLibraryId != null && currentLibraryId != libraryId
        currentLibraryId = libraryId
        currentIsTelevision = isTelevision
        if (_state.value.isOffline) {
            if (switchedLibrary) {
                fetchedItemCount = 0
                _state.update {
                    it.copy(
                        items = emptyList(),
                        hasMore = false,
                        error = null,
                        isShowingCachedCatalog = false,
                        catalogSavedAtEpochMillis = null,
                        catalogTotalItemCount = null,
                    )
                }
            }
            _state.update { it.copy(isLoading = true, isRefreshing = false, error = null) }
            loadJob = viewModelScope.launch {
                val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull()
                if (userId == null) {
                    if (requestGeneration == this@LibraryViewModel.requestGeneration) {
                        _state.update { it.copy(isLoading = false, error = EXPIRED_SESSION_MESSAGE) }
                    }
                    return@launch
                }
                restoreCachedCatalog(userId, libraryId, isTelevision, requestGeneration)
            }
            return
        }
        // Switching libraries must drop the previous catalog. Keeping it would
        // let a failed first page for the new library leave `hasMore` true over
        // the old items, so the next page would be requested at an offset that
        // skips the new library's first items.
        if (switchedLibrary) {
            fetchedItemCount = 0
            _state.update {
                it.copy(
                    items = emptyList(),
                    hasMore = false,
                    error = null,
                    isShowingCachedCatalog = false,
                    catalogSavedAtEpochMillis = null,
                    catalogTotalItemCount = null,
                )
            }
        }
        loadJob = viewModelScope.launch {
            // Compose can request the library immediately after the screen is
            // created. Wait until persisted sort/order/filter preferences have
            // been read so the first server request is already consistent with
            // the user's selection.
            libraryQueryPreferencesReady.filter { it }.first()
            val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: run {
                // Reached when the session is gone and the user observer has not
                // published the null yet. Releasing ownership lets the flag fall,
                // so the screen shows the sign-in message instead of a spinner
                // waiting on a request that nothing will ever start.
                _state.update {
                    it.copy(
                        isLoading = false,
                        isRefreshing = false,
                        error = EXPIRED_SESSION_MESSAGE,
                    )
                }
                return@launch
            }
            _state.update { it.copy(isLoading = true, isRefreshing = true, error = null) }

            // Get library details (name + collection type drive the browse query)
            val libResult = getItemDetailUseCase(userId, libraryId)
            if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@launch
            if (libResult.isFailure && !networkMonitor.isOnline.first()) {
                restoreCachedCatalog(userId, libraryId, isTelevision, requestGeneration)
                return@launch
            }
            val library = libResult.getOrNull()
            val libName = library?.name ?: "Biblioteca"
            currentLibraryName = libName
            currentLibraryCollectionType = library?.collectionType
            if (isTelevision && isBooksLibrary(library)) {
                _state.update {
                    it.copy(
                        libraryName = libName,
                        items = emptyList(),
                        hasMore = false,
                        isLoading = false,
                        isRefreshing = false,
                        error = "A biblioteca de Livros não está disponível na Android TV.",
                    )
                }
                return@launch
            }
            currentIncludeItemTypes = LibraryBrowseTypes.forCollectionType(library?.collectionType)
                .split(',')
                .filterNot { isTelevision && it.trim() in setOf("Book", "Audiobook") }
                .joinToString(",")
            val facets = facetFiltersForRequest(_state.value.activeFilters)

            getLibraryItemsUseCase(
                userId = userId,
                libraryId = libraryId,
                includeItemTypes = currentIncludeItemTypes,
                sortBy = _state.value.sortBy.apiValue,
                sortOrder = _state.value.sortOrder.apiValue,
                startIndex = 0,
                limit = pageSize,
                isPlayed = playedFilter(_state.value.activeFilters),
                isFavorite = favoriteFilter(_state.value.activeFilters),
                genres = facets.genres.ifBlank { null },
                years = facets.years.ifBlank { null },
                officialRatings = facets.officialRatings.ifBlank { null },
            ).onSuccess { (firstPageItems, total) ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@onSuccess
                // "Aleatório" is `ORDER BY RANDOM()` on the server: a second request at
                // an offset would cut a *different* permutation, so there is no page 2
                // to ask for. The whole list has to arrive in one request, and the
                // first response is what tells us how big it is.
                val items = if (supportsOffsetPaging(_state.value.sortBy.apiValue) || total <= firstPageItems.size) {
                    firstPageItems
                } else {
                    val fullCatalogResult = getLibraryItemsUseCase(
                        userId = userId,
                        libraryId = libraryId,
                        includeItemTypes = currentIncludeItemTypes,
                        sortBy = _state.value.sortBy.apiValue,
                        sortOrder = _state.value.sortOrder.apiValue,
                        startIndex = 0,
                        limit = singleRequestItemLimit(_state.value.sortBy.apiValue, pageSize, total),
                        isPlayed = playedFilter(_state.value.activeFilters),
                        isFavorite = favoriteFilter(_state.value.activeFilters),
                        genres = facets.genres.ifBlank { null },
                        years = facets.years.ifBlank { null },
                        officialRatings = facets.officialRatings.ifBlank { null },
                    )
                    val fullCatalogItems = fullCatalogResult.getOrElse { error ->
                        if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@onSuccess
                        if (!networkMonitor.isOnline.first()) {
                            restoreCachedCatalog(userId, libraryId, isTelevision, requestGeneration)
                            return@onSuccess
                        }
                        _state.update {
                            it.copy(
                                isLoading = false,
                                isRefreshing = false,
                                error = error.message ?: "Não foi possível carregar o catálogo completo. Tente novamente.",
                            )
                        }
                        return@onSuccess
                    }.first
                    fullCatalogItems
                }
                if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@onSuccess
                fetchedItemCount = items.size
                _state.update {
                    it.copy(
                        libraryName = libName,
                        items = items,
                        hasMore = supportsOffsetPaging(it.sortBy.apiValue) &&
                            hasMorePages(items.size, items.size, total),
                        isLoading = false,
                        isRefreshing = false,
                        isShowingCachedCatalog = false,
                        catalogSavedAtEpochMillis = null,
                        catalogTotalItemCount = null,
                        error = null,
                    )
                }
                persistLibrarySnapshot(userId, libraryId, libName, library?.collectionType, items, total)
            }.onFailure { error ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@onFailure
                if (!networkMonitor.isOnline.first()) {
                    restoreCachedCatalog(userId, libraryId, isTelevision, requestGeneration)
                    return@onFailure
                }
                _state.update { it.copy(isLoading = false, isRefreshing = false, error = error.message ?: "Não foi possível carregar a biblioteca.") }
            }
        }
    }

    private suspend fun restoreCachedCatalog(
        userId: String,
        libraryId: String,
        isTelevision: Boolean,
        generation: Long,
    ) {
        val snapshot = try {
            libraryCatalogCache.read(userId, libraryId)
        } catch (cancelled: kotlinx.coroutines.CancellationException) {
            throw cancelled
        } catch (_: Exception) {
            null
        }
        if (!isCurrentLibraryRequest(generation, userId, libraryId)) return

        if (isTelevision && snapshot != null && isBooksLibrary(snapshot.collectionType, snapshot.libraryName)) {
            _state.update {
                it.copy(
                    libraryName = snapshot.libraryName,
                    items = emptyList(),
                    hasMore = false,
                    isLoading = false,
                    isRefreshing = false,
                    isShowingCachedCatalog = false,
                    catalogSavedAtEpochMillis = null,
                    catalogTotalItemCount = null,
                    error = "A biblioteca de Livros não está disponível na Android TV.",
                )
            }
            return
        }

        val cachedSort = SortOption.values().firstOrNull { it.apiValue.equals(snapshot?.sortBy, ignoreCase = true) }
        val cachedOrder = SortOrder.values().firstOrNull { it.apiValue.equals(snapshot?.sortOrder, ignoreCase = true) }
        if (snapshot != null) {
            fetchedItemCount = snapshot.items.size
            currentLibraryCollectionType = snapshot.collectionType
            currentLibraryName = snapshot.libraryName
        }
        _state.update {
            it.copy(
                libraryName = snapshot?.libraryName ?: it.libraryName,
                items = snapshot?.items ?: it.items,
                activeFilters = snapshot?.activeFilters?.let(::orderedFilters) ?: it.activeFilters,
                sortBy = cachedSort ?: it.sortBy,
                sortOrder = cachedOrder ?: it.sortOrder,
                hasMore = if (snapshot != null) false else it.hasMore,
                isLoading = false,
                isRefreshing = false,
                isShowingCachedCatalog = snapshot != null,
                catalogSavedAtEpochMillis = snapshot?.savedAtEpochMillis ?: it.catalogSavedAtEpochMillis,
                catalogTotalItemCount = snapshot?.totalItemCount ?: it.catalogTotalItemCount,
                error = when {
                    snapshot != null -> null
                    it.items.isEmpty() -> "Nenhuma lista salva neste dispositivo. Conecte-se para carregar a biblioteca."
                    else -> "Sem conexão. Exibindo os resultados carregados anteriormente."
                },
            )
        }

        if (networkMonitor.isOnline.first()) {
            refreshAfterActiveLoad(generation, libraryId, isTelevision)
        }
    }

    private fun refreshAfterActiveLoad(generation: Long, libraryId: String, isTelevision: Boolean) {
        val activeLoad = loadJob ?: return
        viewModelScope.launch {
            activeLoad.join()
            if (generation == requestGeneration && currentLibraryId == libraryId && !_state.value.isOffline) {
                loadLibrary(libraryId, isTelevision)
            }
        }
    }

    /**
     * Refreshes a visible library only when no request is already active.
     * This is used by the TV foreground timer to avoid cancelling a slow
     * catalog response and replacing it with another request.
     */
    fun refreshIfIdle(
        libraryId: String,
        isTelevision: Boolean = currentIsTelevision,
        deferUntilIdle: Boolean = false,
    ) {
        val current = _state.value
        // `loadLibrary` starts a coroutine before persisted query preferences
        // have necessarily emitted. During that short window the state still
        // reports idle even though a request job already exists. TV enters a
        // library with both the initial load and the foreground refresh effect
        // active, so checking the job prevents the refresh from cancelling the
        // first request and starting a duplicate one.
        if (current.isOffline) return
        if (loadJob?.isActive == true || (currentLibraryId == libraryId && (current.isLoading || current.isRefreshing))) {
            if (deferUntilIdle && currentLibraryId == libraryId) {
                refreshAfterActiveLoad(requestGeneration, libraryId, isTelevision)
            }
            return
        }
        loadLibrary(libraryId, isTelevision, preserveLetterNavigation = true)
    }

    private suspend fun persistLibrarySnapshot(
        userId: String,
        libraryId: String,
        libraryName: String,
        collectionType: String?,
        items: List<MediaItem>,
        totalItemCount: Int,
    ) {
        try {
            libraryCatalogCache.write(
                userId = userId,
                libraryId = libraryId,
                libraryName = libraryName,
                collectionType = collectionType,
                sortBy = _state.value.sortBy.apiValue,
                sortOrder = _state.value.sortOrder.apiValue,
                activeFilters = _state.value.activeFilters,
                items = items,
                totalItemCount = totalItemCount,
            )
        } catch (cancelled: kotlinx.coroutines.CancellationException) {
            throw cancelled
        } catch (_: Exception) {
            // Local persistence is best-effort; it must not break an online catalog.
        }
    }

    fun loadMore() {
        if (_state.value.isOffline) return
        val libId = currentLibraryId ?: run {
            if (currentUserId == null) {
                _state.update {
                    it.copy(
                        isLoading = false,
                        error = EXPIRED_SESSION_MESSAGE,
                    )
                }
            }
            return
        }
        if (!shouldRequestNextPage(_state.value.isLoading, _state.value.hasMore)) return
        // Nothing to page through in a permutation the server redraws per request.
        // `hasMore` is already false for that ordering; this keeps a state that
        // disagrees from issuing the broken offset request.
        if (!supportsOffsetPaging(_state.value.sortBy.apiValue)) return

        // Mark the state before launching the coroutine. Compose can request the
        // sentinel item more than once during a fast scroll/recomposition; the
        // synchronous transition closes that small window and prevents duplicate
        // pages from being appended.
        val requestGeneration = ++this.requestGeneration
        _state.update { it.copy(isLoading = true, error = null) }
        loadJob = viewModelScope.launch {
            val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: run {
                _state.update {
                    it.copy(
                        isLoading = false,
                        error = EXPIRED_SESSION_MESSAGE,
                    )
                }
                return@launch
            }
            if (!isCurrentLibraryRequest(requestGeneration, userId, libId)) {
                // This page no longer owns the state: either a newer request has
                // taken over the flags, or the session went away and nothing is
                // left to lower them. Only the second case is ours to fix.
                if (currentUserId == null) {
                    _state.update { it.copy(isLoading = false) }
                }
                return@launch
            }
            // Page from the number of items actually fetched: the server may cap a
            // page below the requested size, which would otherwise skip items, and
            // the visible list is deduplicated, which would otherwise make the
            // offset stop advancing.
            val requestedStartIndex = fetchedItemCount
            val facets = facetFiltersForRequest(_state.value.activeFilters)
            getLibraryItemsUseCase(
                userId = userId,
                libraryId = libId,
                includeItemTypes = currentIncludeItemTypes,
                sortBy = _state.value.sortBy.apiValue,
                sortOrder = _state.value.sortOrder.apiValue,
                startIndex = requestedStartIndex,
                limit = pageSize,
                isPlayed = playedFilter(_state.value.activeFilters),
                isFavorite = favoriteFilter(_state.value.activeFilters),
                genres = facets.genres.ifBlank { null },
                years = facets.years.ifBlank { null },
                officialRatings = facets.officialRatings.ifBlank { null },
            ).onSuccess { (newItems, total) ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libId)) return@onSuccess
                fetchedItemCount += newItems.size
                val combined = appendDistinctBy(_state.value.items, newItems) { it.id }
                _state.update {
                    it.copy(
                        items = combined,
                        hasMore = hasMorePages(fetchedItemCount, newItems.size, total),
                        isLoading = false,
                        isShowingCachedCatalog = false,
                        catalogSavedAtEpochMillis = null,
                        catalogTotalItemCount = null,
                        error = null,
                    )
                }
                persistLibrarySnapshot(userId, libId, currentLibraryName, currentLibraryCollectionType, combined, total)
            }.onFailure { error ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libId)) return@onFailure
                _state.update { it.copy(isLoading = false, error = error.message ?: "Não foi possível carregar mais itens.") }
            }
        }
    }

    fun navigateToLetter(letter: String) {
        val current = _state.value
        if (current.sortBy != SortOption.Name || current.sortOrder != SortOrder.Ascending) return
        val normalized = letter.uppercase(Locale.ROOT)
        if (normalized != "#" && (normalized.length != 1 || normalized[0] !in 'A'..'Z')) return
        _state.update { it.copy(letterNavigationTarget = normalized) }
    }

    fun finishLetterNavigation(letter: String) {
        _state.update {
            if (it.letterNavigationTarget == letter) it.copy(letterNavigationTarget = null) else it
        }
    }

    fun toggleView() {
        val nextValue = !_state.value.isGridView
        _state.update { it.copy(isGridView = nextValue) }
        viewModelScope.launch { settingsRepository.setLibraryGridViewEnabled(nextValue) }
    }

    fun setGridDensity(density: String) {
        val normalized = normalizeLibraryGridDensity(density)
        _state.update { it.copy(gridDensity = normalized) }
        viewModelScope.launch { settingsRepository.setLibraryGridDensity(normalized) }
    }

    fun showSortMenu() {
        _state.update { it.copy(showSortMenu = true) }
    }

    fun hideSortMenu() {
        _state.update { it.copy(showSortMenu = false) }
    }

    fun showFilterMenu() {
        _state.update { it.copy(showFilterMenu = true) }
        loadFilterOptionsIfNeeded()
    }

    fun hideFilterMenu() {
        _state.update { it.copy(showFilterMenu = false) }
    }

    private fun loadFilterOptionsIfNeeded() {
        val libraryId = currentLibraryId ?: return
        val current = _state.value
        if (current.availableFilterOptions != null || current.isLoadingFilterOptions) return
        if (current.isOffline) {
            _state.update { it.copy(filterOptionsError = "Opções indisponíveis offline. Você ainda pode digitar os filtros.") }
            return
        }

        val generation = ++filterOptionsRequestGeneration
        _state.update { it.copy(isLoadingFilterOptions = true, filterOptionsError = null) }
        filterOptionsJob = viewModelScope.launch {
            val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull()
            if (userId.isNullOrBlank()) {
                if (generation == filterOptionsRequestGeneration) {
                    _state.update {
                        it.copy(
                            isLoadingFilterOptions = false,
                            filterOptionsError = EXPIRED_SESSION_MESSAGE,
                        )
                    }
                }
                return@launch
            }

            getLibraryItemsUseCase.getFilterOptions(
                userId = userId,
                libraryId = libraryId,
                includeItemTypes = currentIncludeItemTypes,
            ).onSuccess { options ->
                if (generation == filterOptionsRequestGeneration && currentLibraryId == libraryId && currentUserId == userId) {
                    _state.update { it.copy(availableFilterOptions = options, isLoadingFilterOptions = false) }
                }
            }.onFailure { error ->
                if (generation == filterOptionsRequestGeneration && currentLibraryId == libraryId && currentUserId == userId) {
                    _state.update {
                        it.copy(
                            isLoadingFilterOptions = false,
                            filterOptionsError = error.message ?: "Não foi possível carregar as opções. Você ainda pode digitar os filtros.",
                        )
                    }
                }
            }
        }
    }

    private fun invalidateFilterOptions() {
        filterOptionsRequestGeneration++
        filterOptionsJob?.cancel()
        filterOptionsJob = null
        _state.update {
            it.copy(
                availableFilterOptions = null,
                isLoadingFilterOptions = false,
                filterOptionsError = null,
            )
        }
    }

    fun toggleFilter(filter: String) {
        val currentFilters = _state.value.activeFilters
        val nextFilters = if (filter in currentFilters) {
            currentFilters - filter
        } else {
            (currentFilters.filterNot { active ->
                (filter == FILTER_PLAYED || filter == FILTER_UNPLAYED) &&
                    (active == FILTER_PLAYED || active == FILTER_UNPLAYED)
            } + filter).distinct()
        }
        _state.update { it.copy(activeFilters = nextFilters, showFilterMenu = false) }
        persistFilters(nextFilters)
        currentLibraryId?.let { loadLibrary(it) }
    }

    fun applyFilters(basicFilters: Collection<String>, facets: LibraryFacetFilters): Boolean {
        val normalizedFacets = normalizeLibraryFacetFilters(facets) ?: return false
        val nextFilters = orderedFilters(activeFiltersWithFacets(basicFilters, normalizedFacets))
        _state.update { it.copy(activeFilters = nextFilters, showFilterMenu = false) }
        persistFilters(nextFilters)
        currentLibraryId?.let { loadLibrary(it) }
        return true
    }

    fun setSortBy(option: SortOption) {
        _state.update { it.copy(sortBy = option, showSortMenu = false) }
        viewModelScope.launch { settingsRepository.setDefaultLibrarySort(option.apiValue) }
        currentLibraryId?.let { loadLibrary(it) }
    }

    fun setSortOrder(order: SortOrder) {
        _state.update { it.copy(sortOrder = order, showSortMenu = false) }
        viewModelScope.launch { settingsRepository.setDefaultLibrarySortOrder(order.apiValue) }
        currentLibraryId?.let { loadLibrary(it) }
    }

    /** Applies the sort field and direction as one library query. */
    fun setSort(option: SortOption, order: SortOrder) {
        _state.update {
            it.copy(
                sortBy = option,
                sortOrder = order,
                showSortMenu = false,
            )
        }
        viewModelScope.launch {
            settingsRepository.setDefaultLibrarySort(option.apiValue)
            settingsRepository.setDefaultLibrarySortOrder(order.apiValue)
        }
        currentLibraryId?.let { loadLibrary(it) }
    }

    fun removeFilter(filter: String) {
        val nextFilters = _state.value.activeFilters - filter
        _state.update { it.copy(activeFilters = nextFilters) }
        persistFilters(nextFilters)
        currentLibraryId?.let { loadLibrary(it) }
    }

    fun clearFilters() {
        _state.update { it.copy(activeFilters = emptyList()) }
        persistFilters(emptyList())
        currentLibraryId?.let { loadLibrary(it) }
    }

    private fun persistFilters(filters: Collection<String>) {
        viewModelScope.launch { settingsRepository.setDefaultLibraryFilters(orderedFilters(filters).toSet()) }
    }

    private fun orderedFilters(filters: Collection<String>): List<String> =
        SUPPORTED_FILTERS.filter { it in filters } + libraryFacetFiltersInServerOrder(filters)

    private fun playedFilter(filters: List<String>): Boolean? = when {
        FILTER_PLAYED in filters -> true
        FILTER_UNPLAYED in filters -> false
        else -> null
    }

    private fun favoriteFilter(filters: List<String>): Boolean? =
        if (FILTER_FAVORITES in filters) true else null

    private fun isCurrentLibraryRequest(
        generation: Long,
        userId: String,
        libraryId: String,
    ): Boolean =
        generation == requestGeneration &&
            currentUserId == userId &&
            currentLibraryId == libraryId

    private fun updateLibraryQueryPreferencesReady() {
        if (sortPreferenceReady && sortOrderPreferenceReady && filtersPreferenceReady) {
            libraryQueryPreferencesReady.value = true
        }
    }
}
