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
import org.mulletaflix.domain.repository.SearchHistoryRepository
import org.mulletaflix.domain.usecase.SearchMediaUseCase
import javax.inject.Inject

data class SearchState(
    val query: String = "",
    val activeFilter: SearchFilter? = null,
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val results: List<MediaItem> = emptyList(),
    val history: List<String> = emptyList(),
    val error: String? = null,
)

@HiltViewModel
class SearchViewModel @Inject constructor(
    private val searchMediaUseCase: SearchMediaUseCase,
    private val authRepository: AuthRepository,
    private val searchHistoryRepository: SearchHistoryRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(SearchState())
    val state: StateFlow<SearchState> = _state.asStateFlow()

    private var searchJob: Job? = null
    private var historyJob: Job? = null
    private var currentUserId: String? = null
    private var searchGeneration = 0L
    private var historyGeneration = 0L

    init {
        viewModelScope.launch {
            authRepository.getSavedUserId().distinctUntilChanged().collect { userId ->
                val userChanged = currentUserId != userId
                currentUserId = userId
                if (userChanged) {
                    searchJob?.cancel()
                    ++searchGeneration
                    _state.update {
                        it.copy(
                            results = emptyList(),
                            isLoading = false,
                            isRefreshing = false,
                            error = null,
                        )
                    }
                }
                val generation = ++historyGeneration
                historyJob?.cancel()
                historyJob = launch {
                    searchHistoryRepository.observeHistory(userId).collect { history ->
                        if (generation == historyGeneration && currentUserId == userId) {
                            _state.update { it.copy(history = history) }
                        }
                    }
                }
            }
        }
    }

    fun onQueryChange(newQuery: String) {
        _state.update { it.copy(query = newQuery, error = null) }
        searchJob?.cancel()
        val generation = ++searchGeneration
        if (newQuery.isBlank()) {
            _state.update { it.copy(results = emptyList(), isLoading = false, error = null) }
            return
        }

        searchJob = viewModelScope.launch {
            delay(350) // debounce
            performSearch(newQuery, _state.value.activeFilter, generation)
        }
    }

    fun search(query: String) {
        val normalizedQuery = normalizeSearchQuery(query) ?: return
        _state.update {
            it.copy(
                query = normalizedQuery,
                history = (listOf(normalizedQuery) + it.history).distinct().take(10),
                error = null,
            )
        }
        viewModelScope.launch {
            searchHistoryRepository.add(currentUserId, normalizedQuery)
        }
        searchJob?.cancel()
        val generation = ++searchGeneration
        searchJob = viewModelScope.launch {
            performSearch(normalizedQuery, _state.value.activeFilter, generation)
        }
    }

    fun retrySearch() {
        val currentQuery = _state.value.query
        if (currentQuery.isNotBlank()) {
            searchJob?.cancel()
            val generation = ++searchGeneration
            searchJob = viewModelScope.launch {
                performSearch(currentQuery, _state.value.activeFilter, generation, isRefresh = false)
            }
        }
    }

    fun refreshSearch() {
        val currentQuery = _state.value.query
        if (currentQuery.isBlank() || _state.value.isLoading || _state.value.isRefreshing) return
        searchJob?.cancel()
        val generation = ++searchGeneration
        searchJob = viewModelScope.launch {
            performSearch(currentQuery, _state.value.activeFilter, generation, isRefresh = true)
        }
    }

    fun setFilter(filter: SearchFilter?) {
        _state.update { it.copy(activeFilter = filter, error = null) }
        val currentQuery = _state.value.query
        if (currentQuery.isNotBlank()) {
            searchJob?.cancel()
            val generation = ++searchGeneration
            searchJob = viewModelScope.launch {
                performSearch(currentQuery, filter, generation, isRefresh = false)
            }
        }
    }

    fun removeHistoryItem(term: String) {
        _state.update { it.copy(history = it.history.filterNot { item -> item == term }) }
        viewModelScope.launch { searchHistoryRepository.remove(currentUserId, term) }
    }

    fun clearHistory() {
        _state.update { it.copy(history = emptyList()) }
        viewModelScope.launch { searchHistoryRepository.clear(currentUserId) }
    }

    private suspend fun performSearch(
        query: String,
        filter: SearchFilter?,
        generation: Long,
        isRefresh: Boolean = false,
    ) {
        val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: run {
            if (generation == searchGeneration &&
                _state.value.query == query &&
                _state.value.activeFilter == filter &&
                currentUserId == null
            ) {
                _state.update { it.copy(isLoading = false, isRefreshing = false, error = "Usuário não autenticado") }
            }
            return
        }
        if (!isCurrentSearch(query, filter, generation, userId)) return
        _state.update {
            it.copy(
                isLoading = !isRefresh,
                isRefreshing = isRefresh,
                error = null,
            )
        }

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
            if (isCurrentSearch(query, filter, generation, userId)) {
                _state.update { it.copy(results = items, isLoading = false, isRefreshing = false, error = null) }
            }
        }.onFailure {
            if (isCurrentSearch(query, filter, generation, userId)) {
                _state.update {
                    it.copy(
                        results = emptyList(),
                        isLoading = false,
                        isRefreshing = false,
                        error = "Erro ao buscar conteúdo",
                    )
                }
            }
        }
    }

    private fun isCurrentSearch(
        query: String,
        filter: SearchFilter?,
        generation: Long,
        userId: String,
    ): Boolean =
        generation == searchGeneration &&
            _state.value.query == query &&
            _state.value.activeFilter == filter &&
            currentUserId == userId
}
