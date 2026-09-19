package org.mulletaflix.feature.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
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
        loadHome()
    }

    fun refresh() {
        loadJob?.cancel()
        _state.update { it.copy(isRefreshing = true) }
        loadHome(refresh = true)
    }

    private fun loadHome(refresh: Boolean = false) {
        loadJob = viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first()
            if (userId.isNullOrBlank()) {
                _state.update {
                    it.copy(
                        isLoading = false,
                        isRefreshing = false,
                        error = "Sessão expirada. Entre novamente para carregar sua biblioteca.",
                    )
                }
                return@launch
            }
            _state.update { it.copy(isLoading = !refresh, error = null) }

            coroutineScope {
                val profileDeferred = async { authRepository.getCurrentUserProfile().getOrNull() }
                val result = getHomeFeedUseCase(userId)
                val profile = profileDeferred.await()

                result.onFailure { error ->
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            userProfile = profile,
                            error = error.userMessage(),
                        )
                    }
                }.onSuccess { feed ->
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
                            userProfile = profile,
                            error = null,
                        )
                    }
                }
            }
        }
    }
}

private fun Throwable.userMessage(): String = when (this) {
    is java.net.UnknownHostException -> "Servidor indisponível. Verifique a conexão com a rede."
    is java.net.ConnectException -> "Não foi possível conectar ao servidor."
    else -> localizedMessage ?: "Não foi possível carregar o conteúdo do servidor."
}
