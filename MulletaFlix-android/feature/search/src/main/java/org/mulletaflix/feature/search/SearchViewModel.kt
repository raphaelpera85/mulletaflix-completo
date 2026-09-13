package org.mulletaflix.feature.search

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.SearchRepository
import javax.inject.Inject

data class SearchState(
    val query: String = "",
    val activeFilter: SearchFilter? = null,
    val isLoading: Boolean = false,
    val results: List<MediaItem> = emptyList(),
    val history: List<String> = emptyList(),
)

@HiltViewModel
class SearchViewModel @Inject constructor(
    private val searchRepository: SearchRepository,
    private val authRepository: AuthRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(SearchState())
    val state: StateFlow<SearchState> = _state.asStateFlow()

    private var searchJob: Job? = null
    private var currentUserId: String? = null

    init {
        viewModelScope.launch {
            authRepository.getSavedUserId().collect { userId ->
                currentUserId = userId
            }
        }
    }

    fun onQueryChange(newQuery: String) {
        _state.update { it.copy(query = newQuery) }
        searchJob?.cancel()
        if (newQuery.isBlank()) {
            _state.update { it.copy(results = emptyList(), isLoading = false) }
            return
        }

        searchJob = viewModelScope.launch {
            delay(350) // debounce
            performSearch(newQuery, _state.value.activeFilter)
        }
    }

    fun search(query: String) {
        searchJob?.cancel()
        _state.update {
            val newHistory = (listOf(query) + it.history).distinct().take(10)
            it.copy(query = query, history = newHistory)
        }
        viewModelScope.launch {
            performSearch(query, _state.value.activeFilter)
        }
    }

    fun setFilter(filter: SearchFilter?) {
        _state.update { it.copy(activeFilter = filter) }
        val currentQuery = _state.value.query
        if (currentQuery.isNotBlank()) {
            viewModelScope.launch {
                performSearch(currentQuery, filter)
            }
        }
    }

    fun clearHistory() {
        _state.update { it.copy(history = emptyList()) }
    }

    private suspend fun performSearch(query: String, filter: SearchFilter?) {
        val userId = currentUserId ?: return
        _state.update { it.copy(isLoading = true) }

        val typeParam = when (filter) {
            SearchFilter.Movies -> "Movie"
            SearchFilter.Series -> "Series"
            SearchFilter.Episodes -> "Episode"
            SearchFilter.Music -> "Audio"
            SearchFilter.Albums -> "MusicAlbum"
            SearchFilter.Artists -> "MusicArtist"
            SearchFilter.People -> "Person"
            null -> null
        }

        searchRepository.searchItems(term = query, userId = userId, itemTypes = typeParam)
            .onSuccess { items ->
                _state.update { it.copy(results = items, isLoading = false) }
            }
            .onFailure {
                _state.update { it.copy(results = emptyList(), isLoading = false) }
            }
    }
}
