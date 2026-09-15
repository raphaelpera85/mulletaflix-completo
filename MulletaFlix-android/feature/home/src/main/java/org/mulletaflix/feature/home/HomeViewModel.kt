package org.mulletaflix.feature.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.Job
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.core.api.SessionRepository
import javax.inject.Inject

data class HomeState(
    val isLoading: Boolean = true,
    val isRefreshing: Boolean = false,
    val heroItem: MediaItem? = null,
    val resumeItems: List<MediaItem> = emptyList(),
    val nextUpItems: List<MediaItem> = emptyList(),
    val recentlyAddedByLibrary: Map<String, List<MediaItem>> = emptyMap(),
    val liveTvChannels: List<MediaItem> = emptyList(),
    val libraries: List<MediaItem> = emptyList(),
    val error: String? = null,
)

@HiltViewModel
class HomeViewModel @Inject constructor(
    private val mediaRepository: MediaRepository,
    private val sessionRepository: SessionRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(HomeState())
    val state: StateFlow<HomeState> = _state.asStateFlow()
    private var loadJob: Job? = null

    init {
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

            val result = try {
                Result.success(coroutineScope {
                    // Parallel fan-out keeps the home responsive on real libraries.
                    val resumeDeferred = async { mediaRepository.getResumeItems(userId) }
                    val nextUpDeferred = async { mediaRepository.getNextUp(userId) }
                    val librariesDeferred = async { mediaRepository.getLibraries(userId) }
                    val liveTvDeferred = async { mediaRepository.getLiveTvChannels(userId) }

                    val resumeResult = resumeDeferred.await()
                    val nextUpResult = nextUpDeferred.await()
                    val librariesResult = librariesDeferred.await()
                    val liveTvResult = liveTvDeferred.await()
                    val libraries = librariesResult.getOrThrow()

                    val recentlyAdded = libraries.map { lib ->
                        async {
                            lib.name to mediaRepository
                                .getLatestItems(userId, parentId = lib.id)
                                .getOrThrow()
                        }
                    }.map { it.await() }.toMap()

                    HomePayload(
                        resumeItems = resumeResult.getOrThrow(),
                        nextUpItems = nextUpResult.getOrThrow(),
                        libraries = libraries,
                        liveTvChannels = liveTvResult.getOrThrow(),
                        recentlyAddedByLibrary = recentlyAdded,
                    )
                })
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (error: Throwable) {
                Result.failure<HomePayload>(error)
            }

            result.onFailure { error ->
                _state.update {
                    it.copy(
                        isLoading = false,
                        isRefreshing = false,
                        error = error.userMessage(),
                    )
                }
            }.onSuccess { payload ->
                val recentlyAdded = payload.recentlyAddedByLibrary

                // Hero = first resume item or first recently added with backdrop.
                val hero = payload.resumeItems.firstOrNull()
                    ?: recentlyAdded.values.flatten().firstOrNull { it.backdropImageTags.isNotEmpty() }

                _state.update {
                    it.copy(
                        isLoading = false,
                        isRefreshing = false,
                        heroItem = hero,
                        resumeItems = payload.resumeItems,
                        nextUpItems = payload.nextUpItems,
                        recentlyAddedByLibrary = recentlyAdded,
                        liveTvChannels = payload.liveTvChannels,
                        libraries = payload.libraries,
                        error = null,
                    )
                }
            }
        }
    }
}

private data class HomePayload(
    val resumeItems: List<MediaItem>,
    val nextUpItems: List<MediaItem>,
    val recentlyAddedByLibrary: Map<String, List<MediaItem>>,
    val liveTvChannels: List<MediaItem>,
    val libraries: List<MediaItem>,
)

private fun Throwable.userMessage(): String = when (this) {
    is java.net.UnknownHostException -> "Servidor indisponível. Verifique a conexão com a rede."
    is java.net.ConnectException -> "Não foi possível conectar ao servidor."
    else -> localizedMessage ?: "Não foi possível carregar o conteúdo do servidor."
}
