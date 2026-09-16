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
import org.mulletaflix.domain.usecase.SearchMediaUseCase
import javax.inject.Inject

data class SearchState(
    val query: String = "",
    val activeFilter: SearchFilter? = null,
    val isLoading: Boolean = false,
    val results: List<MediaItem> = emptyList(),
    val history: List<String> = emptyList(),
    val error: String? = null,
)

@HiltViewModel
class SearchViewModel @Inject constructor(
    private val searchMediaUseCase: SearchMediaUseCase,
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
        _state.update { it.copy(query = newQuery, error = null) }
        searchJob?.cancel()
        if (newQuery.isBlank()) {
            _state.update { it.copy(results = emptyList(), isLoading = false, error = null) }
            return
        }

        searchJob = viewModelScope.launch {
            delay(350) // debounce
            performSearch(newQuery, _state.value.activeFilter)
        }
    }

    fun search(query: String) {
        searchJob?.cancel()
        if (query.isBlank()) return
        _state.update {
            val newHistory = (listOf(query) + it.history).distinct().take(10)
            it.copy(query = query, history = newHistory, error = null)
        }
        viewModelScope.launch {
            performSearch(query, _state.value.activeFilter)
        }
    }

    fun retrySearch() {
        val currentQuery = _state.value.query
        if (currentQuery.isNotBlank()) {
            searchJob?.cancel()
            viewModelScope.launch {
                performSearch(currentQuery, _state.value.activeFilter)
            }
        }
    }

    fun setFilter(filter: SearchFilter?) {
        _state.update { it.copy(activeFilter = filter, error = null) }
        val currentQuery = _state.value.query
        if (currentQuery.isNotBlank()) {
            searchJob?.cancel()
            viewModelScope.launch {
                performSearch(currentQuery, filter)
            }
        }
    }

    fun removeHistoryItem(term: String) {
        _state.update { it.copy(history = it.history.filterNot { item -> item == term }) }
    }

    fun clearHistory() {
        _state.update { it.copy(history = emptyList()) }
    }

    private suspend fun performSearch(query: String, filter: SearchFilter?) {
        val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: run {
            _state.update { it.copy(isLoading = false, error = "Usuário não autenticado") }
            return
        }
        _state.update { it.copy(isLoading = true, error = null) }

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

        searchMediaUseCase(
            userId = userId,
            query = query,
            itemTypes = typeParam,
        ).onSuccess { items ->
            _state.update { it.copy(results = items, isLoading = false, error = null) }
        }.onFailure {
            _state.update { it.copy(results = emptyList(), isLoading = false, error = "Erro ao buscar conteúdo") }
        }
    }
}
