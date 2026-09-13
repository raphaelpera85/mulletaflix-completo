package org.mulletaflix.feature.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.SessionRepository
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

    init {
        loadHome()
    }

    fun refresh() {
        _state.update { it.copy(isRefreshing = true) }
        loadHome(refresh = true)
    }

    private fun loadHome(refresh: Boolean = false) {
        viewModelScope.launch {
            val userId = sessionRepository.getUserId() ?: return@launch
            _state.update { it.copy(isLoading = !refresh, error = null) }

            // Parallel evidence fan-out per fable-loop Stage 2 pattern
            val resumeDeferred = async { mediaRepository.getResumeItems(userId) }
            val nextUpDeferred = async { mediaRepository.getNextUp(userId) }
            val librariesDeferred = async { mediaRepository.getLibraries(userId) }
            val liveTvDeferred = async { mediaRepository.getLiveTvChannels(userId) }

            val resume = resumeDeferred.await().getOrDefault(emptyList())
            val nextUp = nextUpDeferred.await().getOrDefault(emptyList())
            val libraries = librariesDeferred.await().getOrDefault(emptyList())
            val liveChannels = liveTvDeferred.await().getOrDefault(emptyList())

            // Fetch recently added per library (parallel)
            val recentlyAdded = libraries.associate { lib ->
                lib.name to (mediaRepository.getLatestItems(userId, parentId = lib.id).getOrDefault(emptyList()))
            }

            // Hero = first resume item or first recently added with backdrop
            val hero = resume.firstOrNull()
                ?: recentlyAdded.values.flatten().firstOrNull { it.backdropImageTags.isNotEmpty() }

            _state.update {
                it.copy(
                    isLoading = false,
                    isRefreshing = false,
                    heroItem = hero,
                    resumeItems = resume,
                    nextUpItems = nextUp,
                    recentlyAddedByLibrary = recentlyAdded,
                    liveTvChannels = liveChannels,
                    libraries = libraries,
                )
            }
        }
    }
}
