package org.mulletaflix.feature.library

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.domain.model.LibraryBrowseTypes
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.AuthRepository
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
    val hasMore: Boolean = false,
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
) : ViewModel() {

    private val _state = MutableStateFlow(LibraryState())
    val state: StateFlow<LibraryState> = _state.asStateFlow()

    private var currentLibraryId: String? = null
    private var currentUserId: String? = null
    private var hasObservedUser = false
    private var currentIncludeItemTypes: String = LibraryBrowseTypes.DEFAULT
    private val pageSize = 40
    private var loadJob: Job? = null
    private var requestGeneration: Long = 0L
    private var sortPreferenceReady = false
    private var sortOrderPreferenceReady = false
    private var filtersPreferenceReady = false
    private val libraryQueryPreferencesReady = MutableStateFlow(false)

    companion object {
        const val FILTER_FAVORITES = "Favoritos"
        const val FILTER_PLAYED = "Assistidos"
        const val FILTER_UNPLAYED = "Não assistidos"
        private val SUPPORTED_FILTERS = listOf(FILTER_FAVORITES, FILTER_PLAYED, FILTER_UNPLAYED)
    }

    init {
        viewModelScope.launch {
            var previousOnline: Boolean? = null
            networkMonitor.isOnline.distinctUntilChanged().collect { online ->
                _state.update { it.copy(isOffline = !online) }
                if (shouldRefreshLibraryOnNetworkReturn(previousOnline, online)) {
                    currentLibraryId?.let(::refreshIfIdle)
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
                    _state.update {
                        it.copy(
                            items = emptyList(),
                            hasMore = false,
                            isLoading = false,
                            isRefreshing = false,
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

    fun loadLibrary(libraryId: String) {
        loadJob?.cancel()
        val requestGeneration = ++this.requestGeneration
        val switchedLibrary = currentLibraryId != null && currentLibraryId != libraryId
        currentLibraryId = libraryId
        if (_state.value.isOffline) {
            _state.update { it.copy(isLoading = false, isRefreshing = false) }
            return
        }
        // Switching libraries must drop the previous catalog. Keeping it would
        // let a failed first page for the new library leave `hasMore` true over
        // the old items, so the next page would be requested at an offset that
        // skips the new library's first items.
        if (switchedLibrary) {
            _state.update {
                it.copy(
                    items = emptyList(),
                    hasMore = false,
                    error = null,
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
                _state.update { it.copy(isLoading = false, isRefreshing = false, error = "Sessão expirada. Entre novamente.") }
                return@launch
            }
            _state.update { it.copy(isLoading = true, isRefreshing = true, error = null) }

            // Get library details (name + collection type drive the browse query)
            val libResult = getItemDetailUseCase(userId, libraryId)
            if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@launch
            val library = libResult.getOrNull()
            val libName = library?.name ?: "Biblioteca"
            currentIncludeItemTypes = LibraryBrowseTypes.forCollectionType(library?.collectionType)

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
            ).onSuccess { (items, total) ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@onSuccess
                _state.update {
                    it.copy(
                        libraryName = libName,
                        items = items,
                        hasMore = hasMoreLibraryPages(items.size, items.size, total),
                        isLoading = false,
                        isRefreshing = false,
                        error = null,
                    )
                }
            }.onFailure { error ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libraryId)) return@onFailure
                _state.update { it.copy(isLoading = false, isRefreshing = false, error = error.message ?: "Não foi possível carregar a biblioteca.") }
            }
        }
    }

    /**
     * Refreshes a visible library only when no request is already active.
     * This is used by the TV foreground timer to avoid cancelling a slow
     * catalog response and replacing it with another request.
     */
    fun refreshIfIdle(libraryId: String) {
        val current = _state.value
        if (current.isOffline || (currentLibraryId == libraryId && (current.isLoading || current.isRefreshing))) return
        loadLibrary(libraryId)
    }

    fun loadMore() {
        if (_state.value.isOffline) return
        val libId = currentLibraryId ?: run {
            if (currentUserId == null) {
                _state.update {
                    it.copy(
                        isLoading = false,
                        error = "Sessão expirada. Entre novamente.",
                    )
                }
            }
            return
        }
        if (!shouldRequestNextLibraryPage(_state.value.isLoading, _state.value.hasMore)) return

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
                        error = "Sessão expirada. Entre novamente.",
                    )
                }
                return@launch
            }
            if (!isCurrentLibraryRequest(requestGeneration, userId, libId)) return@launch
            // Page from the number of items actually loaded: the server may cap
            // a page below the requested size, which would otherwise skip items.
            val requestedStartIndex = _state.value.items.size
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
            ).onSuccess { (newItems, total) ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libId)) return@onSuccess
                val combined = _state.value.items + newItems
                _state.update {
                    it.copy(
                        items = combined,
                        hasMore = hasMoreLibraryPages(combined.size, newItems.size, total),
                        isLoading = false,
                        error = null,
                    )
                }
            }.onFailure { error ->
                if (!isCurrentLibraryRequest(requestGeneration, userId, libId)) return@onFailure
                _state.update { it.copy(isLoading = false, error = error.message ?: "Não foi possível carregar mais itens.") }
            }
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
    }

    fun hideFilterMenu() {
        _state.update { it.copy(showFilterMenu = false) }
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
        SUPPORTED_FILTERS.filter { it in filters }

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
