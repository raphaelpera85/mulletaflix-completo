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
            _state.update { it.copy(isLoading = true) }

            // Get library details
            val libResult = mediaRepository.getItem(userId, libraryId)
            val libName = libResult.getOrNull()?.name ?: "Biblioteca"

            mediaRepository.getItems(
                userId = userId,
                parentId = libraryId,
                sortBy = _state.value.sortBy.apiValue,
                startIndex = 0,
                limit = pageSize,
            ).onSuccess { (items, total) ->
                totalItems = total
                _state.update {
                    it.copy(
                        libraryName = libName,
                        items = items,
                        hasMore = items.size < total,
                        isLoading = false,
                    )
                }
            }.onFailure {
                _state.update { it.copy(isLoading = false) }
            }
        }
    }

    fun loadMore() {
        val libId = currentLibraryId ?: return
        val userId = currentUserId ?: return
        if (_state.value.isLoading || !_state.value.hasMore) return

        viewModelScope.launch {
            currentStartIndex += pageSize
            mediaRepository.getItems(
                userId = userId,
                parentId = libId,
                sortBy = _state.value.sortBy.apiValue,
                startIndex = currentStartIndex,
                limit = pageSize,
            ).onSuccess { (newItems, total) ->
                val combined = _state.value.items + newItems
                _state.update {
                    it.copy(
                        items = combined,
                        hasMore = combined.size < total,
                    )
                }
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
}
