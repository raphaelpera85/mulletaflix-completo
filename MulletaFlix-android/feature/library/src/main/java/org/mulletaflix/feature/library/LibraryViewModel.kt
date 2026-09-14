package org.mulletaflix.feature.library

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

data class LibraryState(
    val libraryName: String = "Biblioteca",
    val isGridView: Boolean = true,
    val isLoading: Boolean = false,
    val items: List<MediaItem> = emptyList(),
    val activeFilters: List<String> = emptyList(),
    val hasMore: Boolean = false,
    val error: String? = null,
    val showSortMenu: Boolean = false,
    val showFilterMenu: Boolean = false,
    val sortBy: SortOption = SortOption.Name,
)

@HiltViewModel
class LibraryViewModel @Inject constructor(
    private val mediaRepository: MediaRepository,
    private val authRepository: AuthRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(LibraryState())
    val state: StateFlow<LibraryState> = _state.asStateFlow()

    private var currentLibraryId: String? = null
    private var currentUserId: String? = null
    private var currentStartIndex: Int = 0
    private val pageSize = 40
    private var totalItems = 0

    companion object {
        const val FILTER_FAVORITES = "Favoritos"
        const val FILTER_PLAYED = "Assistidos"
        const val FILTER_UNPLAYED = "Não assistidos"
    }

    init {
        viewModelScope.launch {
            authRepository.getSavedUserId().collect { userId ->
                currentUserId = userId
            }
        }
    }

    fun loadLibrary(libraryId: String) {
        currentLibraryId = libraryId
        currentStartIndex = 0
        viewModelScope.launch {
            val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: return@launch
            _state.update { it.copy(isLoading = true, error = null) }

            // Get library details
            val libResult = mediaRepository.getItem(userId, libraryId)
            val libName = libResult.getOrNull()?.name ?: "Biblioteca"

            mediaRepository.getItems(
                userId = userId,
                parentId = libraryId,
                sortBy = _state.value.sortBy.apiValue,
                startIndex = 0,
                limit = pageSize,
                isPlayed = playedFilter(_state.value.activeFilters),
                isFavorite = favoriteFilter(_state.value.activeFilters),
            ).onSuccess { (items, total) ->
                totalItems = total
                _state.update {
                    it.copy(
                        libraryName = libName,
                        items = items,
                        hasMore = items.size < total,
                        isLoading = false,
                        error = null,
                    )
                }
            }.onFailure { error ->
                _state.update { it.copy(isLoading = false, error = error.message ?: "Não foi possível carregar a biblioteca.") }
            }
        }
    }

    fun loadMore() {
        val libId = currentLibraryId ?: return
        val userId = currentUserId ?: return
        if (_state.value.isLoading || !_state.value.hasMore) return

        viewModelScope.launch {
            val requestedStartIndex = currentStartIndex + pageSize
            _state.update { it.copy(isLoading = true, error = null) }
            mediaRepository.getItems(
                userId = userId,
                parentId = libId,
                sortBy = _state.value.sortBy.apiValue,
                startIndex = requestedStartIndex,
                limit = pageSize,
                isPlayed = playedFilter(_state.value.activeFilters),
                isFavorite = favoriteFilter(_state.value.activeFilters),
            ).onSuccess { (newItems, total) ->
                val combined = _state.value.items + newItems
                currentStartIndex = requestedStartIndex
                _state.update {
                    it.copy(
                        items = combined,
                        hasMore = combined.size < total,
                        isLoading = false,
                        error = null,
                    )
                }
            }.onFailure { error ->
                _state.update { it.copy(isLoading = false, error = error.message ?: "Não foi possível carregar mais itens.") }
            }
        }
    }

    fun toggleView() {
        _state.update { it.copy(isGridView = !it.isGridView) }
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
        _state.update {
            val filters = if (filter in it.activeFilters) {
                it.activeFilters - filter
            } else {
                (it.activeFilters.filterNot { active ->
                    (filter == FILTER_PLAYED || filter == FILTER_UNPLAYED) &&
                        (active == FILTER_PLAYED || active == FILTER_UNPLAYED)
                } + filter).distinct()
            }
            it.copy(activeFilters = filters, showFilterMenu = false)
        }
        currentLibraryId?.let { loadLibrary(it) }
    }

    fun setSortBy(option: SortOption) {
        _state.update { it.copy(sortBy = option, showSortMenu = false) }
        currentLibraryId?.let { loadLibrary(it) }
    }

    fun removeFilter(filter: String) {
        _state.update { it.copy(activeFilters = it.activeFilters - filter) }
        currentLibraryId?.let { loadLibrary(it) }
    }

    fun clearFilters() {
        _state.update { it.copy(activeFilters = emptyList()) }
        currentLibraryId?.let { loadLibrary(it) }
    }

    private fun playedFilter(filters: List<String>): Boolean? = when {
        FILTER_PLAYED in filters -> true
        FILTER_UNPLAYED in filters -> false
        else -> null
    }

    private fun favoriteFilter(filters: List<String>): Boolean? =
        if (FILTER_FAVORITES in filters) true else null
}
