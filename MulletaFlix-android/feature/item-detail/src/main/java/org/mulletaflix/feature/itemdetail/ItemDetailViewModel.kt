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
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.repository.PlaylistRepository
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
    val downloadMessage: String? = null,
    val playlists: List<Playlist> = emptyList(),
    val isPlaylistDialogVisible: Boolean = false,
    val playlistMessage: String? = null,
    val isPlaylistLoading: Boolean = false,
)

@HiltViewModel
class ItemDetailViewModel @Inject constructor(
    private val mediaRepository: MediaRepository,
    private val authRepository: AuthRepository,
    private val playbackRepository: PlaybackRepository,
    private val downloadRepository: DownloadRepository,
    private val playlistRepository: PlaylistRepository,
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
                mediaRepository.unmarkAsFavorite(userId, current.id)
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

    fun downloadItem() {
        val userId = currentUserId ?: return
        val item = _state.value.item ?: return
        viewModelScope.launch {
            _state.update { it.copy(downloadMessage = "Preparando download…") }
            playbackRepository.getPlaybackInfo(item.id, userId)
                .mapCatching { it.mediaSources.firstOrNull()?.directStreamUrl ?: error("O servidor não forneceu uma fonte para download.") }
                .fold(
                    onSuccess = { url ->
                        downloadRepository.enqueue(item.id, item.name, url)
                            .onSuccess { _state.update { it.copy(downloadMessage = "Download adicionado à fila.") } }
                            .onFailure { e -> _state.update { it.copy(downloadMessage = e.message ?: "Não foi possível iniciar o download.") } }
                    },
                    onFailure = { e -> _state.update { it.copy(downloadMessage = e.message ?: "Não foi possível preparar o download.") } }
                )
        }
    }

    fun openPlaylistPicker() {
        val userId = currentUserId ?: return
        _state.update { it.copy(isPlaylistDialogVisible = true, isPlaylistLoading = true, playlistMessage = null) }
        viewModelScope.launch {
            playlistRepository.getPlaylists(userId)
                .onSuccess { lists -> _state.update { it.copy(playlists = lists, isPlaylistLoading = false) } }
                .onFailure { error -> _state.update { it.copy(isPlaylistLoading = false, playlistMessage = error.message ?: "Não foi possível carregar as playlists.") } }
        }
    }

    fun closePlaylistPicker() {
        _state.update { it.copy(isPlaylistDialogVisible = false, playlistMessage = null) }
    }

    fun addToPlaylist(playlist: Playlist) {
        val userId = currentUserId ?: return
        val itemId = _state.value.item?.id ?: return
        viewModelScope.launch {
            playlistRepository.addItem(userId, playlist.id, itemId)
                .onSuccess { _state.update { it.copy(isPlaylistDialogVisible = false, playlistMessage = "Adicionado à playlist ${playlist.name}.") } }
                .onFailure { error -> _state.update { it.copy(playlistMessage = error.message ?: "Não foi possível adicionar à playlist.") } }
        }
    }

    fun createPlaylist(name: String) {
        val userId = currentUserId ?: return
        val itemId = _state.value.item?.id ?: return
        viewModelScope.launch {
            playlistRepository.createPlaylist(userId, name, itemId)
                .onSuccess { playlist -> _state.update { it.copy(isPlaylistDialogVisible = false, playlistMessage = "Playlist ${playlist.name} criada.") } }
                .onFailure { error -> _state.update { it.copy(playlistMessage = error.message ?: "Não foi possível criar a playlist.") } }
        }
    }
}
