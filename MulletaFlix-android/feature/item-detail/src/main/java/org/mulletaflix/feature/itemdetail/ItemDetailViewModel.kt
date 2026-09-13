package org.mulletaflix.feature.itemdetail

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

data class ItemDetailState(
    val item: MediaItem? = null,
    val seasons: List<MediaItem> = emptyList(),
    val episodes: List<MediaItem> = emptyList(),
    val selectedSeasonIndex: Int = 0,
    val similarItems: List<MediaItem> = emptyList(),
    val specialFeatures: List<MediaItem> = emptyList(),
    val isLoading: Boolean = false,
    val error: String? = null,
)

@HiltViewModel
class ItemDetailViewModel @Inject constructor(
    private val mediaRepository: MediaRepository,
    private val authRepository: AuthRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(ItemDetailState())
    val state: StateFlow<ItemDetailState> = _state.asStateFlow()

    private var currentUserId: String? = null
    private var currentItemId: String? = null

    init {
        viewModelScope.launch {
            authRepository.getSavedUserId().collect { userId ->
                currentUserId = userId
            }
        }
    }

    fun loadItem(itemId: String) {
        currentItemId = itemId
        viewModelScope.launch {
            val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: return@launch
            _state.update { it.copy(isLoading = true, error = null) }

            mediaRepository.getItem(userId, itemId)
                .onSuccess { mediaItem ->
                    _state.update { it.copy(item = mediaItem, isLoading = false) }

                    // Load similar
                    launch {
                        mediaRepository.getSimilarItems(userId, itemId)
                            .onSuccess { similar -> _state.update { it.copy(similarItems = similar) } }
                    }

                    // If series, load seasons and episodes
                    if (mediaItem.type == MediaItemType.Series) {
                        launch {
                            mediaRepository.getSeasons(userId, itemId)
                                .onSuccess { seasonsList ->
                                    _state.update { it.copy(seasons = seasonsList) }
                                    if (seasonsList.isNotEmpty()) {
                                        selectSeason(0)
                                    }
                                }
                        }
                    }

                    // If music album, load tracks
                    if (mediaItem.type == MediaItemType.MusicAlbum) {
                        launch {
                            mediaRepository.getEpisodes(userId, itemId)
                                .onSuccess { tracks ->
                                    _state.update { it.copy(episodes = tracks) }
                                }
                        }
                    }

                    // Extras / special features
                    launch {
                        mediaRepository.getSpecialFeatures(userId, itemId)
                            .onSuccess { extras -> _state.update { it.copy(specialFeatures = extras) } }
                    }
                }
                .onFailure { err ->
                    _state.update { it.copy(isLoading = false, error = err.localizedMessage ?: "Erro ao carregar detalhes") }
                }
        }
    }

    fun selectSeason(index: Int) {
        val userId = currentUserId ?: return
        val seasons = _state.value.seasons
        if (index !in seasons.indices) return

        _state.update { it.copy(selectedSeasonIndex = index) }
        val season = seasons[index]
        val seriesId = currentItemId ?: return

        viewModelScope.launch {
            mediaRepository.getEpisodes(userId, seriesId, season.id)
                .onSuccess { eps ->
                    _state.update { it.copy(episodes = eps) }
                }
        }
    }

    fun toggleFavorite() {
        val userId = currentUserId ?: return
        val current = _state.value.item ?: return
        val newFav = !current.isFavorite

        _state.update { it.copy(item = current.copy(isFavorite = newFav)) }

        viewModelScope.launch {
            if (newFav) {
                mediaRepository.markAsFavorite(userId, current.id)
            } else {
                mediaRepository.markAsUnplayed(userId, current.id)
            }
        }
    }

    fun toggleWatched() {
        val userId = currentUserId ?: return
        val current = _state.value.item ?: return
        val newWatched = !current.isPlayed

        _state.update { it.copy(item = current.copy(isPlayed = newWatched)) }

        viewModelScope.launch {
            if (newWatched) {
                mediaRepository.markAsPlayed(userId, current.id)
            } else {
                mediaRepository.markAsUnplayed(userId, current.id)
            }
        }
    }
}
