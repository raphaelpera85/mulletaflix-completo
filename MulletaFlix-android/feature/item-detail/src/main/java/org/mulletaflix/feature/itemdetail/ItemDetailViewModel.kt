package org.mulletaflix.feature.itemdetail

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.MediaSource
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import org.mulletaflix.domain.repository.DownloadMediaMetadata
import org.mulletaflix.domain.repository.downloadServerScopeId
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.model.primaryImageUrl
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession
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
    val seasonError: String? = null,
    val error: String? = null,
    val downloadMessage: String? = null,
    val seasonDownloadProgress: SeasonDownloadProgress? = null,
    val interactionMessage: String? = null,
    val isPreparingDownload: Boolean = false,
    val isFavoriteUpdating: Boolean = false,
    val isWatchedUpdating: Boolean = false,
    val playlists: List<Playlist> = emptyList(),
    val isPlaylistDialogVisible: Boolean = false,
    val playlistMessage: String? = null,
    val isPlaylistLoading: Boolean = false,
    val feedbackSessionLoaded: Boolean = false,
    val hasFeedbackSession: Boolean = false,
)

data class SeasonDownloadProgress(
    val seasonId: String,
    val seasonName: String,
    val totalEpisodes: Int,
    val processedEpisodes: Int = 0,
    val queuedEpisodes: Int = 0,
    val alreadyAvailableEpisodes: Int = 0,
    val alreadyPreparingEpisodes: Int = 0,
    val failedEpisodes: Int = 0,
    val isRunning: Boolean = false,
    val isCancelled: Boolean = false,
    val operationId: Long = 0L,
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
    private val sessionRepository: SessionRepository,
    private val userFeedbackRepository: UserFeedbackRepository = object : UserFeedbackRepository {
        override suspend fun requestMedia(session: FeedbackRequestSession, title: String, mediaType: String, year: Int?, notes: String?) = Result.failure<Unit>(UnsupportedOperationException())
        override suspend fun reportPlaybackIssue(session: FeedbackRequestSession, itemId: String, category: String, description: String?) = Result.failure<Unit>(UnsupportedOperationException())
    },
) : ViewModel() {

    private val _state = MutableStateFlow(ItemDetailState())
    val state: StateFlow<ItemDetailState> = _state.asStateFlow()

    private var currentUserId: String? = null
    private var sessionGeneration = 0L
    private var currentSeriesId: String? = null
    private var currentDownloadServerId: String? = null
    private var downloads: List<DownloadEntry> = emptyList()
    private var itemLoadJob: Job? = null
    private var itemRequestGeneration = 0L
    private var seasonLoadJob: Job? = null
    private var seasonDownloadJob: Job? = null
    private var seasonDownloadOperationId = 0L
    private var seasonRequestGeneration = 0L
    private var favoriteJob: Job? = null
    private var watchedJob: Job? = null
    private var favoriteMutationGeneration = 0L
    private var watchedMutationGeneration = 0L
    private var playbackIssueSubmitting = false
    private var feedbackRequestSession: FeedbackRequestSession? = null

    fun reportPlaybackIssue(
        itemId: String,
        category: String,
        description: String,
        onComplete: (Result<Unit>) -> Unit,
    ) {
        if (playbackIssueSubmitting) return
        val requestSession = feedbackRequestSession
        if (requestSession == null) {
            onComplete(Result.failure(IllegalStateException("Sessão indisponível. Conecte-se novamente.")))
            return
        }
        playbackIssueSubmitting = true
        viewModelScope.launch {
            val result = try {
                check(sessionRepository.getFeedbackRequestSession().first() == requestSession) {
                    "A sessão mudou. Revise sua conexão antes de tentar novamente."
                }
                userFeedbackRepository.reportPlaybackIssue(requestSession, itemId, category, description.trim())
            } catch (cancelled: CancellationException) {
                onComplete(Result.failure(cancelled))
                throw cancelled
            } catch (error: Throwable) {
                Result.failure(error)
            } finally {
                playbackIssueSubmitting = false
            }
            result.onSuccess {
                _state.update { it.copy(interactionMessage = "Relato enviado. Obrigado pelo aviso.") }
            }
            onComplete(result)
        }
    }

    init {
        viewModelScope.launch {
            combine(sessionRepository.getServerId(), sessionRepository.getBaseUrl()) { serverId, baseUrl ->
                downloadServerScopeId(serverId, baseUrl)
            }.distinctUntilChanged().collect { currentDownloadServerId = it }
        }
        viewModelScope.launch {
            sessionRepository.getFeedbackRequestSession().distinctUntilChanged().collect { session ->
                feedbackRequestSession = session
                _state.update {
                    it.copy(feedbackSessionLoaded = true, hasFeedbackSession = session != null)
                }
            }
        }
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
                seasonDownloadJob?.cancel()
                favoriteJob?.cancel()
                watchedJob?.cancel()
                itemRequestGeneration++
                seasonRequestGeneration++
                favoriteMutationGeneration++
                watchedMutationGeneration++
                currentSeriesId = null
                val feedbackSessionLoaded = _state.value.feedbackSessionLoaded
                val hasFeedbackSession = _state.value.hasFeedbackSession
                _state.value = ItemDetailState(
                    feedbackSessionLoaded = feedbackSessionLoaded,
                    hasFeedbackSession = hasFeedbackSession,
                )
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
        seasonDownloadJob?.cancel()
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
                    seasonError = null,
                    error = null,
                    downloadMessage = null,
                    seasonDownloadProgress = null,
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
            _state.update { it.copy(isLoadingSeasons = true, seasonError = null) }
            mediaRepository.getSeasons(userId, seriesId)
                .onSuccess { seasonsList ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onSuccess
                    val selectedIndex = seasonsList.indexOfFirst { it.id == initialSeasonId }.coerceAtLeast(0)
                    _state.update {
                        it.copy(
                            seasons = seasonsList,
                            selectedSeasonIndex = selectedIndex,
                            isLoadingSeasons = false,
                            seasonError = null,
                        )
                    }
                    val seasonId = seasonsList.getOrNull(selectedIndex)?.id
                    mediaRepository.getEpisodes(userId, seriesId, seasonId)
                        .onSuccess { eps ->
                            if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onSuccess
                            _state.update { it.copy(episodes = eps, seasonError = null) }
                        }
                        .onFailure {
                            if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onFailure
                            _state.update {
                                it.copy(
                                    isLoadingSeasons = false,
                                    seasonError = "Não foi possível carregar os episódios.",
                                )
                            }
                        }
                }
                .onFailure { error ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onFailure
                    _state.update {
                        it.copy(
                            isLoadingSeasons = false,
                            seasonError = error.localizedMessage ?: "Não foi possível carregar as temporadas.",
                        )
                    }
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
        _state.update {
            it.copy(
                selectedSeasonIndex = index,
                episodes = emptyList(),
                isLoadingSeasons = true,
                seasonError = null,
            )
        }
        val season = seasons[index]

        seasonLoadJob = viewModelScope.launch {
            mediaRepository.getEpisodes(userId, seriesId, season.id)
                .onSuccess { eps ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onSuccess
                    _state.update { it.copy(episodes = eps, isLoadingSeasons = false, seasonError = null) }
                }
                .onFailure { error ->
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration, seasonRequestGeneration)) return@onFailure
                    _state.update {
                        it.copy(
                            isLoadingSeasons = false,
                            seasonError = error.localizedMessage ?: "Não foi possível carregar os episódios.",
                        )
                    }
                }
        }
    }

    fun retrySeriesContext() {
        val userId = currentUserId ?: return
        val seriesId = currentSeriesId ?: return
        val seasonId = _state.value.seasons.getOrNull(_state.value.selectedSeasonIndex)?.id
        loadSeriesContext(userId, seriesId, seasonId)
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

    private suspend fun downloadMediaMetadata(source: MediaSource): DownloadMediaMetadata? {
        val subtitles = downloadableExternalSubtitles(source)
        val serverId = downloadServerScopeId(
            sessionRepository.getServerId().first(),
            sessionRepository.getBaseUrl().first(),
        )
        return serverId?.let {
            DownloadMediaMetadata(it, source.id, subtitles)
        }
    }

    fun downloadItem() {
        val userId = currentUserId ?: return
        val item = _state.value.item ?: return
        if (_state.value.isPreparingDownload) {
            _state.update { it.copy(downloadMessage = "Este download já está sendo preparado.") }
            return
        }
        val requestGeneration = itemRequestGeneration
        val requestSessionGeneration = sessionGeneration
        viewModelScope.launch {
            val serverId = downloadServerScopeId(
                sessionRepository.getServerId().first(),
                sessionRepository.getBaseUrl().first(),
            )
            if (serverId.isNullOrBlank()) {
                _state.update { it.copy(downloadMessage = "Não foi possível identificar o servidor deste download.") }
                return@launch
            }
            val scope = DownloadItemScope(serverId, item.id)
            if (hasActiveDownload(downloads, item.id, serverId)) {
                _state.update { it.copy(downloadMessage = "Este título já está na fila ou disponível offline neste servidor.") }
                return@launch
            }
            if (!preparingDownloadIds.add(scope)) {
                _state.update { it.copy(downloadMessage = "Este título já está sendo preparado para download neste servidor.") }
                return@launch
            }
            _state.update { it.copy(downloadMessage = "Preparando download…", isPreparingDownload = true) }
            try {
                val preparation = runCatching {
                    playbackRepository.getPlaybackInfo(item.id, userId)
                        .mapCatching { playbackInfo ->
                            preferredDownloadSource(playbackInfo.mediaSources)
                                ?: error("O servidor não forneceu uma fonte para download.")
                        }
                }.getOrElse { Result.failure(it) }
                if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@launch
                preparation.fold(
                    onSuccess = { downloadSource ->
                        val seriesId = item.seriesId
                        val seasonNumber = item.parentIndexNumber
                        val episodeNumber = item.indexNumber
                        val episodeMetadata = if (
                            item.type == MediaItemType.Episode &&
                            !seriesId.isNullOrBlank() &&
                            seasonNumber != null &&
                            episodeNumber != null
                        ) {
                            DownloadEpisodeMetadata(
                                seriesId = seriesId,
                                seasonNumber = seasonNumber,
                                episodeNumber = episodeNumber,
                                seriesName = item.seriesName?.takeIf(String::isNotBlank)?.take(512),
                            )
                        } else null
                        val mediaMetadata = downloadMediaMetadata(downloadSource.source)
                        require(mediaMetadata?.serverId == serverId) {
                            "O servidor mudou durante a preparação do download."
                        }
                        if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@launch
                        manageDownloadsUseCase.enqueueWithMediaMetadata(
                            item.id,
                            item.name,
                            downloadSource.url,
                            item.primaryImageUrl,
                            episodeMetadata,
                            mediaMetadata,
                        )
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
                preparingDownloadIds.remove(scope)
                if (isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) {
                    _state.update { it.copy(isPreparingDownload = false) }
                }
            }
        }
    }

    /** Prepares the currently selected season's eligible episodes in series order. */
    fun downloadSelectedSeason(): Long? {
        if (seasonDownloadJob?.isActive == true) return null
        val userId = currentUserId ?: return null
        val serverId = currentDownloadServerId
        if (serverId.isNullOrBlank()) {
            _state.update { it.copy(downloadMessage = "Não foi possível identificar o servidor destes downloads.") }
            return null
        }
        val snapshot = _state.value
        val season = snapshot.seasons.getOrNull(snapshot.selectedSeasonIndex) ?: return null
        val seriesId = currentSeriesId ?: return null
        val episodes = snapshot.episodes
            .asSequence()
            .filter { it.type == MediaItemType.Episode }
            .distinctBy { it.id }
            .toList()
        if (episodes.isEmpty()) {
            _state.update { it.copy(downloadMessage = "Não há episódios disponíveis nesta temporada.") }
            return null
        }

        val alreadyAvailableCount = episodes.count { hasActiveDownload(downloads, it.id, serverId) }
        val alreadyPreparingCount = episodes.count {
            DownloadItemScope(serverId, it.id) in preparingDownloadIds &&
                !hasActiveDownload(downloads, it.id, serverId)
        }
        val pendingEpisodes = episodes.filterNot { episode ->
            hasActiveDownload(downloads, episode.id, serverId) ||
                DownloadItemScope(serverId, episode.id) in preparingDownloadIds
        }
        var queuedCount = 0
        var skippedCount = alreadyAvailableCount + alreadyPreparingCount
        var failedCount = 0
        var processedCount = skippedCount
        val requestGeneration = itemRequestGeneration
        val requestSessionGeneration = sessionGeneration
        val initialProgress = SeasonDownloadProgress(
            seasonId = season.id,
            seasonName = season.name,
            totalEpisodes = episodes.size,
            processedEpisodes = processedCount,
            alreadyAvailableEpisodes = alreadyAvailableCount,
            alreadyPreparingEpisodes = alreadyPreparingCount,
            isRunning = pendingEpisodes.isNotEmpty(),
        )
        if (pendingEpisodes.isEmpty()) {
            _state.update {
                it.copy(
                    seasonDownloadProgress = initialProgress,
                    downloadMessage = "Todos os episódios desta temporada já estão na fila ou disponíveis offline.",
                )
            }
            return null
        }

        val operationId = ++seasonDownloadOperationId
        val activeProgress = initialProgress.copy(operationId = operationId)
        _state.update { it.copy(seasonDownloadProgress = activeProgress, downloadMessage = null) }
        preparingDownloadIds.addAll(pendingEpisodes.map { DownloadItemScope(serverId, it.id) })
        seasonDownloadJob = viewModelScope.launch {
            try {
                for (episode in pendingEpisodes) {
                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@launch

                    val preparedUrl = try {
                        playbackRepository.getPlaybackInfo(episode.id, userId)
                            .mapCatching { playbackInfo ->
                                preferredDownloadSource(playbackInfo.mediaSources)
                                    ?: error("O servidor não forneceu uma fonte para download.")
                            }
                    } catch (cancelled: CancellationException) {
                        throw cancelled
                    } catch (error: Exception) {
                        Result.failure(error)
                    }

                    preparedUrl.exceptionOrNull()?.let { error ->
                        if (error is CancellationException) throw error
                    }

                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@launch
                    preparedUrl.fold(
                        onSuccess = { downloadSource ->
                            if (hasActiveDownload(downloads, episode.id, serverId)) {
                                skippedCount++
                            } else {
                                val episodeMetadata = episode.downloadEpisodeMetadata(seriesId, _state.value.item?.name)
                                val enqueueResult = try {
                                    val mediaMetadata = downloadMediaMetadata(downloadSource.source)
                                    require(mediaMetadata?.serverId == serverId) {
                                        "O servidor mudou durante a preparação do download."
                                    }
                                    if (!isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) return@launch
                                    manageDownloadsUseCase.enqueueWithMediaMetadata(
                                        episode.id,
                                        episode.name,
                                        downloadSource.url,
                                        episode.primaryImageUrl,
                                        episodeMetadata,
                                        mediaMetadata,
                                    )
                                } catch (cancelled: CancellationException) {
                                    throw cancelled
                                } catch (error: Exception) {
                                    Result.failure(error)
                                }
                                enqueueResult.exceptionOrNull()?.let { error ->
                                    if (error is CancellationException) throw error
                                }
                                if (enqueueResult.isSuccess) queuedCount++ else failedCount++
                            }
                        },
                        onFailure = { failedCount++ },
                    )
                    processedCount++
                    _state.update {
                        it.copy(
                            seasonDownloadProgress = activeProgress.copy(
                                processedEpisodes = processedCount,
                                queuedEpisodes = queuedCount,
                                alreadyAvailableEpisodes = skippedCount - alreadyPreparingCount,
                                alreadyPreparingEpisodes = alreadyPreparingCount,
                                failedEpisodes = failedCount,
                            ),
                        )
                    }
                }

                if (isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) {
                    _state.update {
                        it.copy(
                            seasonDownloadProgress = activeProgress.copy(
                                processedEpisodes = processedCount,
                                queuedEpisodes = queuedCount,
                                alreadyAvailableEpisodes = skippedCount - alreadyPreparingCount,
                                alreadyPreparingEpisodes = alreadyPreparingCount,
                                failedEpisodes = failedCount,
                                isRunning = false,
                            ),
                            downloadMessage = seasonDownloadSummary(season.name, queuedCount, skippedCount, failedCount),
                        )
                    }
                }
            } catch (cancelled: CancellationException) {
                if (isCurrentRequest(userId, requestSessionGeneration, requestGeneration)) {
                    _state.update {
                        it.copy(
                            seasonDownloadProgress = activeProgress.copy(
                                processedEpisodes = processedCount,
                                queuedEpisodes = queuedCount,
                                alreadyAvailableEpisodes = skippedCount - alreadyPreparingCount,
                                alreadyPreparingEpisodes = alreadyPreparingCount,
                                failedEpisodes = failedCount,
                                isRunning = false,
                                isCancelled = true,
                            ),
                            downloadMessage = "Preparação cancelada. Os episódios já adicionados permanecem na fila.",
                        )
                    }
                }
                throw cancelled
            } finally {
                pendingEpisodes.forEach { preparingDownloadIds.remove(DownloadItemScope(serverId, it.id)) }
            }
        }
        return operationId
    }

    fun cancelSeasonDownload() {
        seasonDownloadJob?.cancel()
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

    private val preparingDownloadIds = mutableSetOf<DownloadItemScope>()
}

private data class DownloadItemScope(val serverId: String, val itemId: String)

private fun MediaItem.downloadEpisodeMetadata(seriesId: String, fallbackSeriesName: String? = null): DownloadEpisodeMetadata? {
    val resolvedSeriesId = this.seriesId?.takeIf(String::isNotBlank) ?: seriesId
    val seasonNumber = parentIndexNumber ?: return null
    val episodeNumber = indexNumber ?: return null
    val resolvedSeriesName = this.seriesName?.takeIf(String::isNotBlank)
        ?: fallbackSeriesName?.takeIf(String::isNotBlank)
    return DownloadEpisodeMetadata(resolvedSeriesId, seasonNumber, episodeNumber, resolvedSeriesName?.take(512))
}

private fun seasonDownloadSummary(seasonName: String, queued: Int, skipped: Int, failed: Int): String =
    "Temporada $seasonName: $queued episódio(s) adicionado(s) à fila, $skipped já disponível(is) e $failed falha(s)."
