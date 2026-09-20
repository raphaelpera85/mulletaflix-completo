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
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.usecase.GetFavoriteItemsUseCase
import javax.inject.Inject

data class FavoritesState(
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val items: List<MediaItem> = emptyList(),
    val hasMore: Boolean = false,
    val error: String? = null,
)

@HiltViewModel
class FavoritesViewModel @Inject constructor(
    private val getFavoriteItemsUseCase: GetFavoriteItemsUseCase,
    private val authRepository: AuthRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(FavoritesState())
    val state: StateFlow<FavoritesState> = _state.asStateFlow()
    private val pageSize = 40
    private var loadJob: Job? = null
    private var loadGeneration = 0L
    private var loadInFlight = false
    private var currentUserId: String? = null
    private var hasObservedUser = false

    init {
        viewModelScope.launch {
            authRepository.getSavedUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedUser && currentUserId != userId
                currentUserId = userId
                hasObservedUser = true
                if (userChanged) {
                    loadJob?.cancel()
                    loadInFlight = false
                    ++loadGeneration
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

    fun loadMore() {
        val current = _state.value
        if (current.isLoading || !current.hasMore || loadInFlight) return
        load(startIndex = current.items.size, append = true)
    }

    private fun load(startIndex: Int = 0, append: Boolean = false) {
        if (loadInFlight) return
        val generation = ++loadGeneration
        loadInFlight = true
        loadJob = viewModelScope.launch {
            var requestUserId: String? = null
            try {
                val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull()
                requestUserId = userId
                if (userId.isNullOrBlank()) {
                    if (isCurrentLoad(generation, userId)) {
                        _state.update {
                            it.copy(isLoading = false, isRefreshing = false, error = "Sessão expirada. Entre novamente.")
                        }
                    }
                    return@launch
                }

                if (!isCurrentLoad(generation, userId)) return@launch
                _state.update { it.copy(isLoading = true, error = if (append) it.error else null) }
                getFavoriteItemsUseCase(userId, startIndex, pageSize)
                    .onSuccess { (items, total) ->
                        if (!isCurrentLoad(generation, userId)) return@onSuccess
                        _state.update {
                            val merged = if (append) it.items + items else items
                            it.copy(
                                isLoading = false,
                                isRefreshing = false,
                                items = merged,
                                hasMore = merged.size < total,
                                error = null,
                            )
                        }
                    }
                    .onFailure { error ->
                        if (!isCurrentLoad(generation, userId)) return@onFailure
                        _state.update {
                            it.copy(
                                isLoading = false,
                                isRefreshing = false,
                                error = error.message ?: "Não foi possível carregar Minha Lista.",
                            )
                        }
                    }
            } finally {
                if (isCurrentLoad(generation, requestUserId)) loadInFlight = false
            }
        }
    }

    private fun isCurrentLoad(generation: Long, userId: String?): Boolean =
        generation == loadGeneration &&
            !userId.isNullOrBlank() &&
            currentUserId == userId
}
