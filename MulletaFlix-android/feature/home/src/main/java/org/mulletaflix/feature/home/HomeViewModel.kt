package org.mulletaflix.feature.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.Job
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.usecase.GetHomeFeedUseCase
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import javax.inject.Inject

data class HomeState(
    val isLoading: Boolean = true,
    val isRefreshing: Boolean = false,
    val isOffline: Boolean = false,
    val heroItem: MediaItem? = null,
    val resumeItems: List<MediaItem> = emptyList(),
    val nextUpItems: List<MediaItem> = emptyList(),
    val favoriteItems: List<MediaItem> = emptyList(),
    val recentlyAddedByLibrary: Map<String, List<MediaItem>> = emptyMap(),
    val liveTvChannels: List<MediaItem> = emptyList(),
    val libraries: List<MediaItem> = emptyList(),
    val userProfile: UserProfile? = null,
    val error: String? = null,
)

@HiltViewModel
class HomeViewModel @Inject constructor(
    private val getHomeFeedUseCase: GetHomeFeedUseCase,
    private val sessionRepository: SessionRepository,
    private val networkMonitor: NetworkMonitor,
    private val authRepository: AuthRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(HomeState())
    val state: StateFlow<HomeState> = _state.asStateFlow()
    private var loadJob: Job? = null
    private var loadGeneration = 0L
    private var currentUserId: String? = null
    private var hasObservedSession = false

    init {
        viewModelScope.launch {
            var previousOnline: Boolean? = null
            networkMonitor.isOnline.distinctUntilChanged().collect { online ->
                val recovered = shouldRefreshHomeOnNetworkReturn(previousOnline, online)
                previousOnline = online
                _state.update { it.copy(isOffline = !online) }
                if (recovered) refresh()
            }
        }
        viewModelScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedSession && currentUserId != userId
                currentUserId = userId
                hasObservedSession = true
                if (userChanged) {
                    loadJob?.cancel()
                    ++loadGeneration
                    _state.update {
                        it.copy(
                            heroItem = null,
                            resumeItems = emptyList(),
                            nextUpItems = emptyList(),
                            favoriteItems = emptyList(),
                            recentlyAddedByLibrary = emptyMap(),
                            liveTvChannels = emptyList(),
                            libraries = emptyList(),
                            userProfile = null,
                            isLoading = false,
                            isRefreshing = false,
                            error = null,
                        )
                    }
                }
                loadHome()
            }
        }
    }

    fun refresh() {
        loadJob?.cancel()
        _state.update { it.copy(isRefreshing = true) }
        loadHome(refresh = true)
    }

    /**
     * Reconciles the TV home feed without interrupting the initial load or a
     * refresh already in flight. The foreground timer calls this method so a
     * slow server response cannot be cancelled by the next tick.
     */
    fun refreshIfIdle() {
        val current = _state.value
        if (current.isLoading || current.isRefreshing) return
        refresh()
    }

    private fun loadHome(refresh: Boolean = false) {
        val generation = ++loadGeneration
        loadJob = viewModelScope.launch {
            // Do not enqueue a request while the monitor already reports the
            // device offline. Reading the current value here also closes the
            // small startup race between the session collector and the
            // connectivity collector. The network transition collector will
            // trigger a fresh load when connectivity returns.
            val online = networkMonitor.isOnline.first()
            if (!online) {
                if (isCurrentLoad(generation)) {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            isOffline = true,
                        )
                    }
                }
                return@launch
            }
            val userId = currentUserId ?: sessionRepository.getCurrentUserId().first()
            if (userId.isNullOrBlank()) {
                if (isCurrentLoad(generation)) {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            error = "Sessão expirada. Entre novamente para carregar sua biblioteca.",
                        )
                    }
                }
                return@launch
            }
            if (isCurrentLoad(generation)) {
                _state.update { it.copy(isLoading = !refresh, error = null) }
            }

            // Profile/avatar data is secondary to the catalog. Keep it in the
            // same parent job for cancellation, but never make the Home feed
            // wait for a slow profile endpoint before becoming usable.
            val profileJob = launch {
                authRepository.getCurrentUserProfile().getOrNull()?.let { profile ->
                    if (isCurrentLoad(generation)) {
                        _state.update { it.copy(userProfile = profile) }
                    }
                }
            }
            val result = getHomeFeedUseCase(userId)

            if (!isCurrentLoad(generation)) {
                profileJob.cancel()
                return@launch
            }
            result.onFailure { error ->
                if (isCurrentLoad(generation)) {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            error = error.userMessage(),
                        )
                    }
                }
            }.onSuccess { feed ->
                if (isCurrentLoad(generation)) {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            heroItem = feed.heroItem,
                            resumeItems = feed.resumeItems,
                            nextUpItems = feed.nextUpItems,
                            favoriteItems = feed.favoriteItems,
                            recentlyAddedByLibrary = feed.recentlyAddedByLibrary,
                            liveTvChannels = feed.liveTvChannels,
                            libraries = feed.libraries,
                            error = null,
                        )
                    }
                }
            }
        }
    }

    private fun isCurrentLoad(generation: Long): Boolean = generation == loadGeneration
}

private fun Throwable.userMessage(): String = when (this) {
    is java.net.UnknownHostException -> "Servidor indisponível. Verifique a conexão com a rede."
    is java.net.ConnectException -> "Não foi possível conectar ao servidor."
    else -> localizedMessage ?: "Não foi possível carregar o conteúdo do servidor."
}
