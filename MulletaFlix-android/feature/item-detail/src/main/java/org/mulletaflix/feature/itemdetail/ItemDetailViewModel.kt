package org.mulletaflix.feature.itemdetail

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.model.primaryImageUrl
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import org.mulletaflix.domain.usecase.ManagePlaylistUseCase
import org.mulletaflix.domain.usecase.ToggleFavoriteUseCase
import org.mulletaflix.domain.usecase.TogglePlayedUseCase
import javax.inject.Inject

data class ItemDetailState(
    val item: MediaItem? = null,
    val seasons: List<MediaItem> = emptyList(),
    val episodes: List<MediaItem> = emptyList(),
    val selectedSeasonIndex: Int = 0,
    val similarItems: List<MediaItem> = emptyList(),
    val specialFeatures: List<MediaItem> = emptyList(),
    val isLoading: Boolean = false,
    val isLoadingSeasons: Boolean = false,
    val error: String? = null,
    val downloadMessage: String? = null,
    val interactionMessage: String? = null,
    val isPreparingDownload: Boolean = false,
    val isFavoriteUpdating: Boolean = false,
    val isWatchedUpdating: Boolean = false,
    val playlists: List<Playlist> = emptyList(),
    val isPlaylistDialogVisible: Boolean = false,
    val playlistMessage: String? = null,
    val isPlaylistLoading: Boolean = false,
)

@HiltViewModel
class ItemDetailViewModel @Inject constructor(
    private val getItemDetailUseCase: GetItemDetailUseCase,
    private val toggleFavoriteUseCase: ToggleFavoriteUseCase,
    private val togglePlayedUseCase: TogglePlayedUseCase,
    private val manageDownloadsUseCase: ManageDownloadsUseCase,
    private val managePlaylistUseCase: ManagePlaylistUseCase,
    private val mediaRepository: MediaRepository,
    private val authRepository: AuthRepository,
    private val playbackRepository: PlaybackRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(ItemDetailState())
    val state: StateFlow<ItemDetailState> = _state.asStateFlow()

    private var currentUserId: String? = null
    private var sessionGeneration = 0L
    private var currentSeriesId: String? = null
    private var downloads: List<DownloadEntry> = emptyList()
    private var itemLoadJob: Job? = null
    private var itemRequestGeneration = 0L
    private var seasonLoadJob: Job? = null
    private var seasonRequestGeneration = 0L
    private var favoriteJob: Job? = null
    private var watchedJob: Job? = null
    private var favoriteMutationGeneration = 0L
    private var watchedMutationGeneration = 0L

    init {
        viewModelScope.launch {
            authRepository.getSavedUserId().distinctUntilChanged().collect { userId ->
                if (currentUserId == null && !userId.isNullOrBlank()) {
                    currentUserId = userId
                    return@collect
                }
                if (currentUserId == userId) return@collect
                currentUserId = userId
                sessionGeneration++
                itemLoadJob?.cancel()
                seasonLoadJob?.cancel()
                favoriteJob?.cancel()
                watchedJob?.cancel()
                itemRequestGeneration++
                seasonRequestGeneration++
                favoriteMutationGeneration++
                watchedMutationGeneration++
                currentSeriesId = null
                _state.value = ItemDetailState()
            }
        }
        viewModelScope.launch {
            manageDownloadsUseCase.observeDownloads().collect { entries ->
                downloads = entries
            }
        }
    }

    fun loadItem(itemId: String) {
        itemLoadJob?.cancel()
        seasonLoadJob?.cancel()
        favoriteJob?.cancel()
        watchedJob?.cancel()
        val requestGeneration = ++itemRequestGeneration
        seasonRequestGeneration++
        favoriteMutationGeneration++
        watchedMutationGeneration++
        currentSeriesId = null
        itemLoadJob = viewModelScope.launch {
            val userId = currentUserId ?: authRepository.getSavedUserId().firstOrNull() ?: return@launch
            if (currentUserId == null) currentUserId = userId
            val requestSessionGeneration = sessionGeneration
            _state.update {
                it.copy(
                    item = null,
                    seasons = emptyList(),
                    episodes = emptyList(),
                    selectedSeasonIndex = 0,
                    similarItems = emptyList(),
                    specialFeatures = emptyList(),
                    isLoading = true,
                    isLoadingSeasons = false,
                    error = null,
                    downloadMessage = null,
                    interactionMessage = null,
                    isPreparingDownload = false,
                    isFavoriteUpdating = false,
                    isWatchedUpdating = false,
                )
            }

            getItemDetailUseCase(userId, itemId)
                .onSuccess { mediaItem ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@onSuccess
                    _state.update { it.copy(item = mediaItem, isLoading = false) }

                    // Load similar
                    launch {
                        mediaRepository.getSimilarItems(userId, itemId)
                            .onSuccess { similar ->
                                if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@onSuccess
                                _state.update { it.copy(similarItems = similar) }
                            }
                    }

                    // Series context: season tabs + episodes. Seasons and episodes
                    // open inside their series instead of as standalone items.
                    when (mediaItem.type) {
                        MediaItemType.Series -> loadSeriesContext(userId, itemId, null)
                        MediaItemType.Season -> mediaItem.seriesId?.let { seriesId ->
                            loadSeriesContext(userId, seriesId, mediaItem.id)
                        }
                        MediaItemType.Episode -> mediaItem.seriesId?.let { seriesId ->
                            loadSeriesContext(userId, seriesId, mediaItem.seasonId)
                        }
                        else -> Unit
                    }

                    // If music album, load tracks (albums are not series: a
                    // Shows/{id}/Episodes query would answer 404 for an album).
                    if (mediaItem.type == MediaItemType.MusicAlbum) {
                        launch {
                            mediaRepository.getItems(
                                userId = userId,
                                parentId = itemId,
                                includeItemTypes = "Audio",
                                sortBy = "ParentIndexNumber,IndexNumber,SortName",
                            ).onSuccess { (tracks, _) ->
                                if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@onSuccess
                                _state.update { it.copy(episodes = tracks) }
                            }
                        }
                    }

                    // Extras / special features
                    launch {
                        mediaRepository.getSpecialFeatures(userId, itemId)
                            .onSuccess { extras ->
                                if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@onSuccess
                                _state.update { it.copy(specialFeatures = extras) }
                            }
                    }
                }
                .onFailure { err ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@onFailure
                    _state.update { it.copy(isLoading = false, error = err.localizedMessage ?: "Erro ao carregar detalhes") }
                }
        }
    }

    /**
     * Loads the season selector and the episode list of [seriesId].
     *
     * When the screen was opened from a season or an episode, [initialSeasonId]
     * selects the tab that contains it. A series whose episodes are not grouped
     * into seasons still shows all of its episodes (no tab row).
     */
    private fun loadSeriesContext(userId: String, seriesId: String, initialSeasonId: String?) {
        seasonLoadJob?.cancel()
        val requestGeneration = ++seasonRequestGeneration
        currentSeriesId = seriesId
        val requestSessionGeneration = sessionGeneration
        seasonLoadJob = viewModelScope.launch {
            _state.update { it.copy(isLoadingSeasons = true) }
            mediaRepository.getSeasons(userId, seriesId)
                .onSuccess { seasonsList ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onSuccess
                    val selectedIndex = seasonsList.indexOfFirst { it.id == initialSeasonId }.coerceAtLeast(0)
                    _state.update {
                        it.copy(
                            seasons = seasonsList,
                            selectedSeasonIndex = selectedIndex,
                            isLoadingSeasons = false,
                        )
                    }
                    val seasonId = seasonsList.getOrNull(selectedIndex)?.id
                    mediaRepository.getEpisodes(userId, seriesId, seasonId)
                        .onSuccess { eps ->
                            if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onSuccess
                            _state.update { it.copy(episodes = eps) }
                        }
                }
                .onFailure {
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onFailure
                    _state.update { it.copy(isLoadingSeasons = false) }
                }
        }
    }

    fun selectSeason(index: Int) {
        val userId = currentUserId ?: return
        val seasons = _state.value.seasons
        if (index !in seasons.indices) return
        val seriesId = currentSeriesId ?: return

        seasonLoadJob?.cancel()
        val requestGeneration = ++seasonRequestGeneration
        val requestSessionGeneration = sessionGeneration
        _state.update { it.copy(selectedSeasonIndex = index, isLoadingSeasons = true) }
        val season = seasons[index]

        seasonLoadJob = viewModelScope.launch {
            mediaRepository.getEpisodes(userId, seriesId, season.id)
                .onSuccess { eps ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onSuccess
                    _state.update { it.copy(episodes = eps, isLoadingSeasons = false) }
                }
                .onFailure {
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onFailure
                    _state.update { it.copy(isLoadingSeasons = false) }
                }
        }
    }

    fun toggleFavorite() {
        val userId = currentUserId ?: return
        val current = _state.value.item ?: return
        if (_state.value.isFavoriteUpdating) return
        val newFav = !current.isFavorite
        val mutationGeneration = ++favoriteMutationGeneration
        val mutationSessionGeneration = sessionGeneration

        _state.update {
            it.copy(item = current.copy(isFavorite = newFav), isFavoriteUpdating = true)
        }

        favoriteJob?.cancel()
        favoriteJob = viewModelScope.launch {
            toggleFavoriteUseCase(userId, current.id, current.isFavorite)
                .onSuccess { updatedFav ->
                    if (isCurrentMutation(userId, mutationSessionGeneration, mutationGeneration, favoriteMutationGeneration) && _state.value.item?.id == current.id) {
                        _state.update {
                            it.copy(
                                item = current.copy(isFavorite = updatedFav),
                                interactionMessage = if (updatedFav) "Adicionado aos favoritos." else "Removido dos favoritos.",
                            )
                        }
                    }
                }
                .onFailure { error ->
                    if (isCurrentMutation(userId, mutationSessionGeneration, mutationGeneration, favoriteMutationGeneration) && _state.value.item?.id == current.id) {
                        _state.update {
                            it.copy(
                                item = current,
                                interactionMessage = "Não foi possível atualizar os favoritos: ${error.userMessage()}",
                            )
                        }
                    }
                }
            if (isCurrentMutation(userId, mutationSessionGeneration, mutationGeneration, favoriteMutationGeneration) && _state.value.item?.id == current.id) {
                _state.update { it.copy(isFavoriteUpdating = false) }
            }
        }
    }

    fun toggleWatched() {
        val userId = currentUserId ?: return
        val current = _state.value.item ?: return
        if (_state.value.isWatchedUpdating) return
        val newWatched = !current.isPlayed
        val mutationGeneration = ++watchedMutationGeneration
        val mutationSessionGeneration = sessionGeneration

        _state.update {
            it.copy(item = current.copy(isPlayed = newWatched), isWatchedUpdating = true)
        }

        watchedJob?.cancel()
        watchedJob = viewModelScope.launch {
            togglePlayedUseCase(userId, current.id, current.isPlayed)
                .onSuccess { updatedPlayed ->
                    if (isCurrentMutation(userId, mutationSessionGeneration, mutationGeneration, watchedMutationGeneration) && _state.value.item?.id == current.id) {
                        _state.update {
                            it.copy(
                                item = current.copy(isPlayed = updatedPlayed),
                                interactionMessage = if (updatedPlayed) "Marcado como assistido." else "Marcado como não assistido.",
                            )
                        }
                    }
                }
                .onFailure { error ->
                    if (isCurrentMutation(userId, mutationSessionGeneration, mutationGeneration, watchedMutationGeneration) && _state.value.item?.id == current.id) {
                        _state.update {
                            it.copy(
                                item = current,
                                interactionMessage = "Não foi possível atualizar o status: ${error.userMessage()}",
                            )
                        }
                    }
                }
            if (isCurrentMutation(userId, mutationSessionGeneration, mutationGeneration, watchedMutationGeneration) && _state.value.item?.id == current.id) {
                _state.update { it.copy(isWatchedUpdating = false) }
            }
        }
    }

    fun downloadItem() {
        val userId = currentUserId ?: return
        val item = _state.value.item ?: return
        if (_state.value.isPreparingDownload) {
            _state.update { it.copy(downloadMessage = "Este download já está sendo preparado.") }
            return
        }
        if (hasActiveDownload(downloads, item.id)) {
            _state.update { it.copy(downloadMessage = "Este título já está na fila ou disponível offline.") }
            return
        }
        val requestGeneration = itemRequestGeneration
        val requestSessionGeneration = sessionGeneration
        viewModelScope.launch {
            _state.update { it.copy(downloadMessage = "Preparando download…", isPreparingDownload = true) }
            try {
                val preparation = runCatching {
                    playbackRepository.getPlaybackInfo(item.id, userId)
                        .mapCatching { playbackInfo ->
                            preferredDownloadUrl(playbackInfo.mediaSources)
                                ?: error("O servidor não forneceu uma fonte para download.")
                        }
                }.getOrElse { Result.failure(it) }
                if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@launch
                preparation.fold(
                    onSuccess = { url ->
                        manageDownloadsUseCase.enqueueWithMetadata(item.id, item.name, url, item.primaryImageUrl)
                            .onSuccess {
                                if (isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) {
                                    _state.update { it.copy(downloadMessage = "Download adicionado à fila.") }
                                }
                            }
                            .onFailure { e ->
                                if (isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) {
                                    _state.update { it.copy(downloadMessage = e.message ?: "Não foi possível iniciar o download.") }
                                }
                            }
                    },
                    onFailure = { e ->
                        if (isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) {
                            _state.update { it.copy(downloadMessage = e.message ?: "Não foi possível preparar o download.") }
                        }
                    },
                )
            } finally {
                if (isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) {
                    _state.update { it.copy(isPreparingDownload = false) }
                }
            }
        }
    }

    fun openPlaylistPicker() {
        val userId = currentUserId ?: return
        val requestSessionGeneration = sessionGeneration
        _state.update { it.copy(isPlaylistDialogVisible = true, isPlaylistLoading = true, playlistMessage = null) }
        viewModelScope.launch {
            managePlaylistUseCase.getPlaylists(userId)
                .onSuccess { lists ->
                    if (!isCurrentSession(userId, requestSessionGeneration)) return@onSuccess
                    _state.update { it.copy(playlists = lists, isPlaylistLoading = false) }
                }
                .onFailure { error ->
                    if (!isCurrentSession(userId, requestSessionGeneration)) return@onFailure
                    _state.update { it.copy(isPlaylistLoading = false, playlistMessage = error.message ?: "Não foi possível carregar as playlists.") }
                }
        }
    }

    fun closePlaylistPicker() {
        _state.update { it.copy(isPlaylistDialogVisible = false, playlistMessage = null) }
    }

    fun addToPlaylist(playlist: Playlist) {
        val userId = currentUserId ?: return
        val itemId = _state.value.item?.id ?: return
        val requestSessionGeneration = sessionGeneration
        viewModelScope.launch {
            managePlaylistUseCase.addToPlaylist(userId, playlist.id, itemId)
                .onSuccess {
                    if (!isCurrentSession(userId, requestSessionGeneration)) return@onSuccess
                    _state.update { it.copy(isPlaylistDialogVisible = false, playlistMessage = "Adicionado à playlist ${playlist.name}.") }
                }
                .onFailure { error ->
                    if (!isCurrentSession(userId, requestSessionGeneration)) return@onFailure
                    _state.update { it.copy(playlistMessage = error.message ?: "Não foi possível adicionar à playlist.") }
                }
        }
    }

    fun createPlaylist(name: String) {
        val userId = currentUserId ?: return
        val itemId = _state.value.item?.id ?: return
        val requestSessionGeneration = sessionGeneration
        viewModelScope.launch {
            managePlaylistUseCase.createPlaylist(userId, name, itemId)
                .onSuccess { playlist ->
                    if (!isCurrentSession(userId, requestSessionGeneration)) return@onSuccess
                    _state.update { it.copy(isPlaylistDialogVisible = false, playlistMessage = "Playlist ${playlist.name} criada.") }
                }
                .onFailure { error ->
                    if (!isCurrentSession(userId, requestSessionGeneration)) return@onFailure
                    _state.update { it.copy(playlistMessage = error.message ?: "Não foi possível criar a playlist.") }
                }
        }
    }

    private fun isCurrentSession(userId: String, requestSessionGeneration: Long): Boolean =
        currentUserId == userId && sessionGeneration == requestSessionGeneration

    private fun isCurrentRequest(
        userId: String,
        requestSessionGeneration: Long,
        requestGeneration: Long,
        currentRequestGeneration: Long = itemRequestGeneration,
    ): Boolean = isCurrentSession(userId, requestSessionGeneration) &&
        currentRequestGeneration == requestGeneration

    private fun isCurrentMutation(
        userId: String,
        mutationSessionGeneration: Long,
        mutationGeneration: Long,
        currentMutationGeneration: Long,
    ): Boolean = isCurrentSession(userId, mutationSessionGeneration) &&
        currentMutationGeneration == mutationGeneration

    private fun Throwable.userMessage(): String = message?.takeIf { it.isNotBlank() } ?: "tente novamente."
}
