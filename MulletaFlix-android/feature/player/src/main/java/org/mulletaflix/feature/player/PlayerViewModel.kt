package org.mulletaflix.feature.player

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.media3.common.MediaItem as Media3Item
import androidx.media3.common.Player
import androidx.media3.common.C
import androidx.media3.common.PlaybackException
import androidx.media3.common.util.UnstableApi
import androidx.media3.common.TrackSelectionOverride
import androidx.media3.cast.CastPlayer
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.exoplayer.trackselection.DefaultTrackSelector
import androidx.media3.datasource.DefaultDataSource
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.datasource.cache.CacheDataSource
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import android.content.Context
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.model.Chapter
import org.mulletaflix.domain.model.MediaSegment
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetNextEpisodeUseCase
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.OfflineDownloadCache
import javax.inject.Inject

data class TrackInfo(val index: Int, val displayName: String)

data class NextEpisodeInfo(
    val id: String,
    val title: String,
    val episodeNumber: Int?,
    val seasonNumber: Int?,
)

data class PlayerState(
    val title: String? = null,
    val isPlaying: Boolean = false,
    val isBuffering: Boolean = false,
    val currentPosition: Long = 0L,   // ms
    val duration: Long = 0L,          // ms
    val playbackSpeed: Float = 1f,
    val selectedQuality: String? = "Auto",
    val availableQualities: List<String> = emptyList(),
    val subtitleTracks: List<TrackInfo> = emptyList(),
    val selectedSubtitleIndex: Int = -1,
    val audioTracks: List<TrackInfo> = emptyList(),
    val selectedAudioIndex: Int = 0,
    val subtitleFontSize: Int = 100,
    val pictureInPictureEnabled: Boolean = true,
    val showSkipIntro: Boolean = false,
    val showSkipCredits: Boolean = false,
    val skipTargetPosition: Long? = null,
    val nextEpisode: NextEpisodeInfo? = null,
    val nextEpisodeCountdown: Int? = null,
    val error: String? = null,
    val aspectRatio: VideoAspectRatio = VideoAspectRatio.FIT,
    val isControlsLocked: Boolean = false,
    val estimatedEndTime: String? = null,
    val currentChapterName: String? = null,
    val chapters: List<Chapter> = emptyList(),
    val playbackStats: PlaybackStats? = null,
)

@HiltViewModel
@UnstableApi
class PlayerViewModel @Inject constructor(
    @ApplicationContext context: Context,
    private val getItemDetailUseCase: GetItemDetailUseCase,
    private val playbackRepository: PlaybackRepository,
    private val sessionRepository: SessionRepository,
    private val settingsRepository: SettingsRepository,
    private val getNextEpisodeUseCase: GetNextEpisodeUseCase,
) : ViewModel() {

    private val _state = MutableStateFlow(PlayerState())
    val state: StateFlow<PlayerState> = _state.asStateFlow()

    private val trackSelector = DefaultTrackSelector(context)
    private val localPlayer: ExoPlayer = ExoPlayer.Builder(context)
        .setTrackSelector(trackSelector)
        .setMediaSourceFactory(
            DefaultMediaSourceFactory(
                CacheDataSource.Factory()
                    .setCache(OfflineDownloadCache.get(context))
                    .setUpstreamDataSourceFactory(DefaultHttpDataSource.Factory())
            )
        )
        .build().also { exo ->
        exo.addListener(object : Player.Listener {
            override fun onIsPlayingChanged(isPlaying: Boolean) {
                if (isPlaying) playbackRetryCount = 0
                _state.update { it.copy(isPlaying = isPlaying) }
                if (isPlaying) {
                    startProgressReporting()
                } else {
                    stopProgressReporting()
                    reportPlaybackProgress(isPaused = true)
                }
            }

            override fun onPlaybackStateChanged(playbackState: Int) {
                _state.update {
                    it.copy(isBuffering = playbackState == Player.STATE_BUFFERING)
                }
                if (playbackState == Player.STATE_ENDED) {
                    reportPlaybackStopped()
                    handlePlaybackEnded()
                }
            }

            override fun onTracksChanged(tracks: androidx.media3.common.Tracks) {
                pendingAudioStreamIndex?.let { index ->
                    selectTrackByServerIndex(index, C.TRACK_TYPE_AUDIO)
                    pendingAudioStreamIndex = null
                }
                if (pendingSubtitlesDisabled) {
                    selectSubtitle(-1)
                    pendingSubtitlesDisabled = false
                } else {
                    pendingSubtitleStreamIndex?.let { index ->
                        selectTrackByServerIndex(index, C.TRACK_TYPE_TEXT)
                        pendingSubtitleStreamIndex = null
                    }
                }
            }

            override fun onPlayerError(error: PlaybackException) {
                val transcodeUrl = currentTranscodeUrl
                if (shouldFallbackToTranscode(
                        currentUri = localPlayer.currentMediaItem?.localConfiguration?.uri?.toString(),
                        transcodeUri = transcodeUrl,
                        alreadyTried = triedTranscodeFallback,
                    )
                ) {
                    triedTranscodeFallback = true
                    val positionAtError = fallbackPosition(localPlayer.currentPosition)
                    _state.update { it.copy(isBuffering = true, error = null) }
                    player.setMediaItem(Media3Item.Builder().setUri(transcodeUrl!!).build())
                    player.prepare()
                    player.seekTo(positionAtError)
                    player.play()
                    return
                }
                val itemIdAtError = currentItemId
                val uriAtError = localPlayer.currentMediaItem?.localConfiguration?.uri
                if (itemIdAtError != null && shouldRetryPlayback(error.errorCode, playbackRetryCount)) {
                    playbackRetryCount += 1
                    val positionAtError = localPlayer.currentPosition
                    retryJob?.cancel()
                    _state.update { it.copy(isBuffering = true, error = null) }
                    retryJob = viewModelScope.launch {
                        delay(750)
                        if (currentItemId == itemIdAtError &&
                            localPlayer.currentMediaItem?.localConfiguration?.uri == uriAtError
                        ) {
                            player.prepare()
                            player.seekTo(positionAtError)
                            player.play()
                        }
                    }
                    return
                }
                _state.update {
                    it.copy(isBuffering = false, error = error.localizedMessage ?: "Não foi possível reproduzir esta mídia.")
                }
            }
        })
    }

    /** Media3's unified player keeps the same controls for local and Cast output. */
    val player: CastPlayer = CastPlayer.Builder(context)
        .setLocalPlayer(localPlayer)
        .build()

    private val mediaSession: androidx.media3.session.MediaSession =
        PlayerMediaSessionBridge.attach(context, player)

    private var progressJob: Job? = null
    private var serverProgressJob: Job? = null
    private var loadJob: Job? = null
    private var retryJob: Job? = null
    private var currentItemId: String? = null
    private var currentPlaySessionId: String? = null
    private var currentMediaSourceId: String? = null
    private var currentTranscodeUrl: String? = null
    private var triedTranscodeFallback = false
    private var playbackRetryCount = 0
    private var stoppedReported = false
    private var autoPlayEnabled = true
    private var skipIntroEnabled = true
    private var defaultQuality = "Auto"
    private var defaultPlaybackSpeed = 1f
    private var subtitleFontSize = 100
    private var pendingAudioStreamIndex: Int? = null
    private var pendingSubtitleStreamIndex: Int? = null
    private var pendingSubtitlesDisabled = false
    /** Global stream indexes required by the server's playback reporting API. */
    private var currentAudioStreamIndex: Int? = null
    private var currentSubtitleStreamIndex: Int? = null

    init {
        viewModelScope.launch {
            settingsRepository.isAutoPlayEnabled().collect { autoPlayEnabled = it }
        }
        viewModelScope.launch {
            settingsRepository.isSkipIntroEnabled().collect { skipIntroEnabled = it }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultQuality().collect { defaultQuality = it }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultPlaybackSpeed().collect { defaultPlaybackSpeed = it }
        }
        viewModelScope.launch {
            settingsRepository.getSubtitleFontSize().collect { size ->
                subtitleFontSize = size
                _state.update { it.copy(subtitleFontSize = size) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isPiPEnabled().collect { enabled ->
                _state.update { it.copy(pictureInPictureEnabled = enabled) }
            }
        }
    }

    fun loadMedia(itemId: String) {
        loadJob?.cancel()
        retryJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        player.stop()
        currentItemId = itemId
        currentItemChapters = emptyList()
        currentItemSegments = emptyList()
        stoppedReported = false
        currentTranscodeUrl = null
        currentAudioStreamIndex = null
        currentSubtitleStreamIndex = null
        triedTranscodeFallback = false
        playbackRetryCount = 0
        _state.update {
            it.copy(
                title = null,
                isPlaying = false,
                isBuffering = true,
                currentPosition = 0L,
                duration = 0L,
                nextEpisode = null,
                nextEpisodeCountdown = null,
                error = null,
            )
        }
        loadJob = viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first()
            if (userId == null) {
                showLoadError("Faça login para reproduzir esta mídia.")
                return@launch
            }

            // Get playback info from server to determine best play method
            val item = getItemDetailUseCase(userId, itemId).getOrElse {
                showLoadError("Não foi possível carregar os dados desta mídia.")
                return@launch
            }
            if (currentItemId != itemId) return@launch
            currentItemChapters = item.chapters
            _state.update { it.copy(title = item.name, chapters = item.chapters, error = null) }

            // Fetch Intro Skipper / native media segments
            viewModelScope.launch {
                playbackRepository.getMediaSegments(itemId).onSuccess { segments ->
                    if (currentItemId == itemId) {
                        currentItemSegments = segments
                    }
                }
            }

            // Check for next episode in series
            viewModelScope.launch {
                getNextEpisodeUseCase(userId, item).onSuccess { next ->
                    if (next != null && currentItemId == itemId) {
                        _state.update {
                            it.copy(
                                nextEpisode = NextEpisodeInfo(
                                    id = next.id,
                                    title = next.name,
                                    episodeNumber = next.indexNumber,
                                    seasonNumber = next.parentIndexNumber,
                                )
                            )
                        }
                    }
                }
            }

            val playbackInfo = playbackRepository.getPlaybackInfo(
                itemId = itemId,
                userId = userId,
                startTimeTicks = item.userProgress?.playbackPositionTicks,
            ).getOrElse {
                showLoadError("O servidor não conseguiu preparar esta mídia.")
                return@launch
            }
            val mediaSource = playbackInfo.mediaSources.firstOrNull() ?: run {
                showLoadError("Nenhuma fonte de reprodução está disponível para esta mídia.")
                return@launch
            }
            val streamUrl = mediaSource.directStreamUrl
                ?: mediaSource.transcodeUrl
                ?: run {
                    showLoadError("O servidor não forneceu uma URL de reprodução.")
                    return@launch
                }
            if (currentItemId != itemId) return@launch
            currentPlaySessionId = playbackInfo.playSessionId
            currentMediaSourceId = mediaSource.id
            currentTranscodeUrl = mediaSource.transcodeUrl

            // Some Jellyfin-compatible servers include streams only in PlaybackInfo.
            // Prefer those streams and fall back to the item details when necessary.
            val mediaStreams = mediaSource.mediaStreams.ifEmpty { item.mediaStreams }
            val subtitleStreams = mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Subtitle }
            val audioStreams = mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Audio }
            val preferredAudioLanguage = settingsRepository.getPreferredAudioLanguage().first()
            val preferredSubtitleLanguage = settingsRepository.getPreferredSubtitleLanguage().first()
            val preferredAudioStreamIndex = preferredStreamIndex(
                streams = audioStreams,
                preferredLanguage = preferredAudioLanguage,
                serverDefaultIndex = mediaSource.defaultAudioStreamIndex,
            )
            val preferredSubtitleStreamIndex = preferredStreamIndex(
                streams = subtitleStreams,
                preferredLanguage = preferredSubtitleLanguage,
                serverDefaultIndex = mediaSource.defaultSubtitleStreamIndex,
            )
            pendingAudioStreamIndex = preferredAudioStreamIndex
            pendingSubtitleStreamIndex = preferredSubtitleStreamIndex
            currentAudioStreamIndex = preferredAudioStreamIndex
            currentSubtitleStreamIndex = preferredSubtitleStreamIndex
            pendingSubtitlesDisabled = preferredSubtitleLanguage.equals("off", ignoreCase = true) ||
                preferredSubtitleLanguage.equals("none", ignoreCase = true)

            val subtitleTracks = subtitleStreams
                .mapIndexed { i, stream -> TrackInfo(stream.index, stream.displayTitle ?: stream.displayLanguage ?: stream.language ?: "Legenda ${i + 1}") }

            val audioTracks = audioStreams
                .mapIndexed { i, stream -> TrackInfo(stream.index, stream.displayTitle ?: stream.displayLanguage ?: stream.language ?: "Áudio ${i + 1}") }

            val selectedAudioIndex = uiTrackIndex(
                tracks = audioTracks,
                serverStreamIndex = preferredAudioStreamIndex,
                fallback = 0,
            )
            val selectedSubtitleIndex = uiTrackIndex(
                tracks = subtitleTracks,
                serverStreamIndex = preferredSubtitleStreamIndex,
                fallback = -1,
            )

            val videoStream = mediaStreams.firstOrNull { it.type == org.mulletaflix.domain.model.MediaStreamType.Video }
            val audioStream = mediaStreams.firstOrNull { it.type == org.mulletaflix.domain.model.MediaStreamType.Audio }
            val stats = PlaybackStats(
                videoCodec = videoStream?.codec?.uppercase(),
                audioCodec = audioStream?.codec?.uppercase(),
                resolution = if ((videoStream?.width ?: 0) > 0 && (videoStream?.height ?: 0) > 0) "${videoStream?.width}x${videoStream?.height}" else null,
                bitrate = videoStream?.bitRate?.let { "${it / 1000} kbps" },
                playMethod = if (mediaSource.transcodeUrl != null && streamUrl == mediaSource.transcodeUrl) "Transcode" else "Direct Play",
            )

            _state.update {
                it.copy(
                    subtitleTracks = subtitleTracks,
                    audioTracks = audioTracks,
                    selectedSubtitleIndex = selectedSubtitleIndex,
                    selectedAudioIndex = selectedAudioIndex.coerceAtLeast(0),
                    selectedQuality = defaultQuality,
                    availableQualities = qualityOptions(mediaStreams),
                    showSkipIntro = false,
                    showSkipCredits = false,
                    skipTargetPosition = null,
                    playbackStats = stats,
                )
            }

            // Prepare Media3. CastPlayer automatically transfers this item when a
            // compatible Cast route is selected by the user.
            val mediaItem = Media3Item.Builder()
                .setUri(streamUrl)
                .build()

            player.setMediaItem(mediaItem)
            player.prepare()
            applyDefaultPlaybackPreferences()

            // Resume from last position
            item.userProgress?.playbackPositionTicks?.let { ticks ->
                val positionMs = ticks / 10_000L
                if (positionMs > 0) player.seekTo(positionMs)
            }

            if (autoPlayEnabled) player.play()

            // Report start to server
            playbackRepository.reportPlaybackStart(
                itemId = itemId,
                playSessionId = currentPlaySessionId,
                mediaSourceId = currentMediaSourceId,
                audioIndex = currentAudioStreamIndex,
                subtitleIndex = currentSubtitleStreamIndex,
                positionTicks = player.currentPosition * 10_000L,
            )
        }
    }

    /** Plays a completed Media3 download through the shared cache, without server calls. */
    fun loadOffline(uri: String, title: String) {
        loadJob?.cancel()
        retryJob?.cancel()
        progressJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        player.stop()
        currentItemId = null
        currentPlaySessionId = null
        currentMediaSourceId = null
        currentTranscodeUrl = null
        currentAudioStreamIndex = null
        currentSubtitleStreamIndex = null
        triedTranscodeFallback = true
        playbackRetryCount = 0
        _state.value = PlayerState(title = title, isBuffering = true, error = null)
        player.setMediaItem(Media3Item.Builder().setUri(uri).build())
        player.prepare()
        applyDefaultPlaybackPreferences()
        player.play()
    }

    fun togglePlayPause() {
        if (player.isPlaying) player.pause() else player.play()
    }

    fun seekTo(positionMs: Long) {
        player.seekTo(positionMs)
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first() ?: return@launch
            currentItemId?.let { id ->
                playbackRepository.reportPlaybackProgress(
                    itemId = id,
                    playSessionId = currentPlaySessionId,
                    mediaSourceId = currentMediaSourceId,
                    audioIndex = currentAudioStreamIndex,
                    subtitleIndex = currentSubtitleStreamIndex,
                    positionTicks = positionMs * 10_000L,
                    isPaused = !player.isPlaying,
                )
            }
        }
    }

    /** Updates the local player while the seek bar is being dragged.
     * The server report is intentionally deferred until [seekTo] is called.
     */
    fun previewSeekTo(positionMs: Long) {
        player.seekTo(positionMs)
        _state.update { it.copy(currentPosition = positionMs.coerceAtLeast(0L)) }
    }

    fun skipPrevious() { player.seekToPreviousMediaItem() }

    fun skipNext() {
        val next = _state.value.nextEpisode
        if (next != null) {
            playNextEpisodeNow()
        } else {
            player.seekToNextMediaItem()
        }
    }

    fun skipSegment() {
        _state.value.skipTargetPosition?.let(player::seekTo)
        _state.update { it.copy(showSkipIntro = false, showSkipCredits = false) }
    }

    fun selectSubtitle(index: Int) {
        _state.update { it.copy(selectedSubtitleIndex = index) }
        if (index < 0) {
            currentSubtitleStreamIndex = null
            selectTrackByServerIndex(-1, C.TRACK_TYPE_TEXT)
            return
        }
        val serverIndex = serverTrackIndexAt(_state.value.subtitleTracks, index) ?: return
        currentSubtitleStreamIndex = serverIndex
        selectTrackByServerIndex(serverIndex, C.TRACK_TYPE_TEXT)
    }

    fun selectAudio(index: Int) {
        _state.update { it.copy(selectedAudioIndex = index) }
        val serverIndex = serverTrackIndexAt(_state.value.audioTracks, index) ?: return
        currentAudioStreamIndex = serverIndex
        selectTrackByServerIndex(serverIndex, C.TRACK_TYPE_AUDIO)
    }

    fun selectQuality(quality: String) {
        _state.update { it.copy(selectedQuality = quality) }
        val constraint = videoQualityConstraint(quality)
        localPlayer.trackSelectionParameters = localPlayer.trackSelectionParameters
            .buildUpon()
            .setMaxVideoSize(constraint.maxWidth, constraint.maxHeight)
            .setMaxVideoBitrate(constraint.maxBitrate)
            .build()
    }

    fun setPlaybackSpeed(speed: Float) {
        player.setPlaybackSpeed(speed)
        _state.update { it.copy(playbackSpeed = speed) }
    }

    fun setAspectRatio(ratio: VideoAspectRatio) {
        _state.update { it.copy(aspectRatio = ratio) }
    }

    fun toggleControlsLock() {
        _state.update { it.copy(isControlsLocked = !it.isControlsLocked) }
    }

    fun setControlsLocked(locked: Boolean) {
        _state.update { it.copy(isControlsLocked = locked) }
    }

    fun skipToNextChapter() {
        findNextChapterPosition(currentItemChapters, player.currentPosition)?.let(::seekTo)
    }

    fun skipToPreviousChapter() {
        findPreviousChapterPosition(currentItemChapters, player.currentPosition)?.let(::seekTo)
    }

    private fun applyDefaultPlaybackPreferences() {
        selectQuality(defaultQuality)
        setPlaybackSpeed(defaultPlaybackSpeed.coerceIn(0.5f, 2f))
    }

    fun startCast() {
        // The visible MediaRouteButton opens the official system chooser. This
        // method remains as a semantic hook for custom controls and keeps the
        // current position ready for a transfer.
        if (player.currentMediaItem == null) return
        player.seekTo(player.currentPosition)
    }

    /** Applies a server-global stream index; UI positions are mapped by callers. */
    private fun selectTrackByServerIndex(index: Int, trackType: Int) {
        if (index < 0) {
            if (trackType == C.TRACK_TYPE_TEXT) {
                localPlayer.trackSelectionParameters = localPlayer.trackSelectionParameters
                    .buildUpon()
                    .setTrackTypeDisabled(trackType, true)
                    .build()
            }
            return
        }

        val candidates = player.currentTracks.groups
            .filter { it.type == trackType }
            .flatMap { group ->
                (0 until group.length).mapNotNull { trackIndex ->
                    if (group.isTrackSupported(trackIndex)) group to trackIndex else null
                }
            }
        val selected = candidates.firstOrNull { (group, trackIndex) ->
            group.getTrackFormat(trackIndex).id?.toIntOrNull() == index
        } ?: candidates.firstOrNull { (_, trackIndex) -> trackIndex == index }
            ?: return
        localPlayer.trackSelectionParameters = localPlayer.trackSelectionParameters
            .buildUpon()
            .setTrackTypeDisabled(trackType, false)
            .setOverrideForType(TrackSelectionOverride(selected.first.mediaTrackGroup, selected.second))
            .build()
    }

    private fun startProgressReporting() {
        progressJob?.cancel()
        serverProgressJob?.cancel()
        progressJob = viewModelScope.launch {
            while (isActive) {
                val position = player.currentPosition.coerceAtLeast(0L)
                val duration = player.duration.coerceAtLeast(0L)
                val currentChapter = findCurrentChapter(currentItemChapters, position)
                val endTime = calculateEstimatedEndTime(position, duration, _state.value.playbackSpeed)
                _state.update {
                    val skip = if (skipIntroEnabled) {
                        skipAction(currentItemSegments, currentItemChapters, position)
                    } else {
                        null
                    }
                    it.copy(
                        currentPosition = position,
                        duration = duration,
                        showSkipIntro = skip?.kind == ChapterSkipKind.INTRO,
                        showSkipCredits = skip?.kind == ChapterSkipKind.CREDITS,
                        skipTargetPosition = skip?.targetPositionMs,
                        estimatedEndTime = endTime,
                        currentChapterName = currentChapter?.name,
                    )
                }
                delay(250)
            }
        }
        serverProgressJob = viewModelScope.launch {
            while (isActive) {
                delay(5_000)
                val userId = sessionRepository.getCurrentUserId().first() ?: continue
                currentItemId?.let { id ->
                    val position = player.currentPosition.coerceAtLeast(0L)
                    playbackRepository.reportPlaybackProgress(
                        itemId = id,
                        playSessionId = currentPlaySessionId,
                        mediaSourceId = currentMediaSourceId,
                        audioIndex = null,
                        subtitleIndex = null,
                        positionTicks = position * 10_000L,
                        isPaused = false,
                    )
                }
            }
        }
    }

    private fun stopProgressReporting() {
        progressJob?.cancel()
        progressJob = null
        serverProgressJob?.cancel()
        serverProgressJob = null
    }

    private fun reportPlaybackProgress(isPaused: Boolean) {
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first() ?: return@launch
            currentItemId?.let { id ->
                playbackRepository.reportPlaybackProgress(
                    itemId = id,
                    playSessionId = currentPlaySessionId,
                    mediaSourceId = currentMediaSourceId,
                    positionTicks = player.currentPosition * 10_000L,
                    audioIndex = currentAudioStreamIndex,
                    subtitleIndex = currentSubtitleStreamIndex,
                    isPaused = isPaused,
                )
            }
        }
    }

    private fun reportPlaybackStopped() {
        if (stoppedReported) return
        stoppedReported = true
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first() ?: return@launch
            currentItemId?.let { id ->
                playbackRepository.reportPlaybackStopped(
                    itemId = id,
                    playSessionId = currentPlaySessionId,
                    mediaSourceId = currentMediaSourceId,
                    positionTicks = player.currentPosition * 10_000L,
                )
            }
        }
    }

    private var nextEpisodeCountdownJob: Job? = null

    private fun handlePlaybackEnded() {
        val next = _state.value.nextEpisode ?: return
        if (!autoPlayEnabled) return
        nextEpisodeCountdownJob?.cancel()
        nextEpisodeCountdownJob = viewModelScope.launch {
            for (i in 5 downTo 1) {
                _state.update { it.copy(nextEpisodeCountdown = i) }
                delay(1000)
            }
            _state.update { it.copy(nextEpisodeCountdown = null) }
            loadMedia(next.id)
        }
    }

    fun playNextEpisodeNow() {
        val next = _state.value.nextEpisode ?: return
        nextEpisodeCountdownJob?.cancel()
        _state.update { it.copy(nextEpisodeCountdown = null) }
        loadMedia(next.id)
    }

    fun cancelNextEpisodeCountdown() {
        nextEpisodeCountdownJob?.cancel()
        _state.update { it.copy(nextEpisodeCountdown = null) }
    }

    override fun onCleared() {
        loadJob?.cancel()
        retryJob?.cancel()
        progressJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        reportPlaybackStopped()
        PlayerMediaSessionBridge.detach(mediaSession)
        player.release()
        localPlayer.release()
    }

    private fun showLoadError(message: String) {
        if (currentItemId != null) {
            _state.update { it.copy(isBuffering = false, isPlaying = false, error = message) }
        }
    }

    private var currentItemChapters: List<Chapter> = emptyList()
    private var currentItemSegments: List<MediaSegment> = emptyList()
}
