package org.mulletaflix.feature.library

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.firstOrNull
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.paging.appendDistinctBy
import org.mulletaflix.domain.paging.hasMorePages
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.usecase.GetFavoriteItemsUseCase
import javax.inject.Inject

data class FavoritesState(
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val items: List<MediaItem> = emptyList(),
    val hasMore: Boolean = false,
    val error: String? = null,
    val gridDensity: String = LIBRARY_GRID_DENSITY_COMFORTABLE,
)

@HiltViewModel
class FavoritesViewModel @Inject constructor(
    private val getFavoriteItemsUseCase: GetFavoriteItemsUseCase,
    private val authRepository: AuthRepository,
    private val settingsRepository: SettingsRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(FavoritesState())
    val state: StateFlow<FavoritesState> = _state.asStateFlow()
    private val pageSize = 40
    private var loadJob: Job? = null
    private var loadGeneration = 0L
    private var loadInFlight = false
    private var currentUserId: String? = null
    private var hasObservedUser = false

    /**
     * Items the server has handed over, which is the offset the next page starts at.
     *
     * Not `items.size`: the visible list drops entries whose id is already present, so
     * a page that arrived as an overlap would leave the offset standing still and the
     * list would re-request the same window forever.
     */
    private var fetchedItemCount = 0

    init {
        viewModelScope.launch {
            settingsRepository.getLibraryGridDensity().collect { density ->
                _state.update { it.copy(gridDensity = normalizeLibraryGridDensity(density)) }
            }
        }
        viewModelScope.launch {
            authRepository.getSavedUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedUser && currentUserId != userId
                currentUserId = userId
                hasObservedUser = true
                if (userChanged) {
                    loadJob?.cancel()
                    loadInFlight = false
                    ++loadGeneration
                    fetchedItemCount = 0
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
                load()
            }
        }
    }

    fun refresh() {
        loadJob?.cancel()
        loadInFlight = false
        _state.update { it.copy(isRefreshing = true) }
        load()
    }

    /**
     * Used by the TV foreground timer. A periodic reconciliation must not
     * cancel a slow response that is already bringing the user's list up to
     * date.
     */
    fun refreshIfIdle() {
        val current = _state.value
        if (loadInFlight || current.isLoading || current.isRefreshing) return
        refresh()
    }

    fun loadMore() {
        val current = _state.value
        if (current.isLoading || !current.hasMore || loadInFlight) return
        load(startIndex = fetchedItemCount, append = true)
    }

    private fun load(startIndex: Int = 0, append: Boolean = false) {
        if (loadInFlight) return
        val generation = ++loadGeneration
        loadInFlight = true
        loadJob = viewModelScope.launch {
            try {
                val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull()
                if (userId.isNullOrBlank()) {
                    if (isCurrentLoad(generation)) {
                        _state.update {
                            it.copy(isLoading = false, isRefreshing = false, error = "Sessão expirada. Entre novamente.")
                        }
                    }
                    return@launch
                }

                if (!isCurrentLoad(generation)) return@launch
                _state.update { it.copy(isLoading = true, error = if (append) it.error else null) }
                getFavoriteItemsUseCase(userId, startIndex, pageSize)
                    .onSuccess { (items, total) ->
                        if (!isCurrentLoad(generation)) return@onSuccess
                        fetchedItemCount = if (append) fetchedItemCount + items.size else items.size
                        _state.update {
                            // A window that shifted on the server (an item added or
                            // removed between two requests) returns the tail of the
                            // previous page as the head of this one. The grid renders
                            // `key = item.id`, so the duplicate has to go.
                            val merged = if (append) {
                                appendDistinctBy(it.items, items) { item -> item.id }
                            } else {
                                items
                            }
                            it.copy(
                                isLoading = false,
                                isRefreshing = false,
                                items = merged,
                                // An empty page against a stale total must stop
                                // pagination instead of keeping the sentinel
                                // loading and re-requesting the same offset. The
                                // count is the raw one, because the visible list
                                // drops duplicates.
                                hasMore = hasMorePages(fetchedItemCount, items.size, total),
                                error = null,
                            )
                        }
                    }
                    .onFailure { error ->
                        if (!isCurrentLoad(generation)) return@onFailure
                        _state.update {
                            it.copy(
                                isLoading = false,
                                isRefreshing = false,
                                error = error.message ?: "Não foi possível carregar Minha Lista.",
                            )
                        }
                    }
            } finally {
                // Identity by generation, not by user. `refresh()` cancels this
                // coroutine and immediately starts another one for the same user;
                // cancelling does not run `finally` until the next dispatch, so a
                // user comparison can still be true here and release the flag the
                // replacement just claimed.
                if (isCurrentLoad(generation)) loadInFlight = false
            }
        }
    }

    private fun isCurrentLoad(generation: Long): Boolean = generation == loadGeneration
}
