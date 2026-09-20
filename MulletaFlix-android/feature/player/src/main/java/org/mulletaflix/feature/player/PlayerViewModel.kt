package org.mulletaflix.feature.player

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import android.net.Uri
import androidx.media3.common.MediaItem as Media3Item
import androidx.media3.common.MediaMetadata
import androidx.media3.common.AudioAttributes
import androidx.media3.common.Player
import androidx.media3.common.C
import androidx.media3.common.PlaybackException
import androidx.media3.common.util.UnstableApi
import androidx.media3.common.TrackSelectionOverride
import androidx.media3.cast.CastPlayer
import com.google.android.gms.cast.framework.CastContext
import com.google.android.gms.cast.framework.CastSession
import com.google.android.gms.cast.framework.SessionManager
import com.google.android.gms.cast.framework.SessionManagerListener
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
import kotlinx.coroutines.flow.distinctUntilChanged
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
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.domain.model.primaryImageUrl
import javax.inject.Inject

data class TrackInfo(
    val index: Int,
    val displayName: String,
    val language: String? = null,
    val codec: String? = null,
    val channels: Int? = null,
    val isDefault: Boolean = false,
    val isForced: Boolean = false,
)

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
    val selectedAudioIndex: Int = -1,
    val subtitleFontSize: Int = 100,
    val subtitleColor: String = SUBTITLE_COLOR_WHITE,
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
    val isNetworkOffline: Boolean = false,
    val sleepTimerRemainingMs: Long? = null,
    val sleepTimerMinutes: Int? = null,
    val sleepTimerMode: SleepTimerMode = SleepTimerMode.OFF,
    val isCasting: Boolean = false,
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
    private val networkMonitor: NetworkMonitor,
) : ViewModel() {

    private val offlinePlaybackPositions = context.getSharedPreferences(
        "offline_playback_positions",
        Context.MODE_PRIVATE,
    )

    private val _state = MutableStateFlow(PlayerState())
    val state: StateFlow<PlayerState> = _state.asStateFlow()

    private val trackSelector = DefaultTrackSelector(context)
    private val localPlayer: ExoPlayer = ExoPlayer.Builder(context)
        .setTrackSelector(trackSelector)
        .setAudioAttributes(
            AudioAttributes.Builder()
                .setUsage(C.USAGE_MEDIA)
                .setContentType(C.AUDIO_CONTENT_TYPE_MOVIE)
                .build(),
            true,
        )
        .setHandleAudioBecomingNoisy(true)
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
                    val keys = listOfNotNull(localPlaybackKey, legacyLocalPlaybackKey)
                    if (keys.isNotEmpty()) {
                        offlinePlaybackPositions.edit().apply {
                            keys.forEach(::remove)
                        }.apply()
                    }
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
                lastPlaybackErrorCode = error.errorCode
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
                    player.setMediaItem(
                        Media3Item.Builder()
                            .setUri(transcodeUrl!!)
                            .setMediaMetadata(currentMediaMetadata ?: MediaMetadata.EMPTY)
                            .build(),
                    )
                    player.prepare()
                    player.seekTo(positionAtError)
                    player.play()
                    return
                }
                val itemIdAtError = currentItemId
                val uriAtError = localPlayer.currentMediaItem?.localConfiguration?.uri
                if (itemIdAtError != null && shouldRetryPlayback(error.errorCode, playbackRetryCount)) {
                    val retryAttempt = playbackRetryCount
                    playbackRetryCount += 1
                    val positionAtError = localPlayer.currentPosition
                    val generationAtError = playbackLoadGeneration
                    retryJob?.cancel()
                    _state.update { it.copy(isBuffering = true, error = null) }
                    retryJob = viewModelScope.launch {
                        delay(playbackRetryDelayMs(retryAttempt))
                        if (isCurrentPlaybackLoad(
                                expectedGeneration = generationAtError,
                                currentGeneration = playbackLoadGeneration,
                                expectedItemId = itemIdAtError,
                                currentItemId = currentItemId,
                            ) &&
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
                    it.copy(
                        isBuffering = false,
                        error = userFacingPlaybackError(error.errorCode, error.localizedMessage),
                    )
                }
            }
        })
    }

    /** Media3's unified player keeps the same controls for local and Cast output. */
    val player: CastPlayer = CastPlayer.Builder(context)
        .setLocalPlayer(localPlayer)
        .build()

    private val castSessionManager: SessionManager? = runCatching {
        CastContext.getSharedInstance(context).sessionManager
    }.getOrNull()

    private val castSessionListener = object : SessionManagerListener<CastSession> {
        override fun onSessionStarting(session: CastSession) = updateCastState(true)
        override fun onSessionStarted(session: CastSession, sessionId: String) = updateCastState(true)
        override fun onSessionStartFailed(session: CastSession, error: Int) = updateCastState(false)
        override fun onSessionEnding(session: CastSession) = updateCastState(false)
        override fun onSessionEnded(session: CastSession, error: Int) = updateCastState(false)
        override fun onSessionResuming(session: CastSession, sessionId: String) = updateCastState(true)
        override fun onSessionResumed(session: CastSession, wasSuspended: Boolean) = updateCastState(true)
        override fun onSessionResumeFailed(session: CastSession, error: Int) = updateCastState(false)
        override fun onSessionSuspended(session: CastSession, reason: Int) = updateCastState(false)
    }

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
    private var currentMediaMetadata: MediaMetadata? = null
    private var triedTranscodeFallback = false
    private var playbackRetryCount = 0
    private var playbackLoadGeneration = 0L
    private var currentUserId: String? = null
    private var hasObservedSession = false
    private var sessionGeneration = 0L
    private var networkWasOffline = false
    private var lastPlaybackErrorCode: Int? = null
    private var stoppedReported = false
    private var autoPlayEnabled = true
    private var skipIntroEnabled = true
    private var defaultQuality = "Auto"
    private var defaultPlaybackSpeed = 1f
    private var defaultAspectRatio = VideoAspectRatio.FIT
    private var subtitleFontSize = 100
    private var subtitleColor = SUBTITLE_COLOR_WHITE
    private var pendingAudioStreamIndex: Int? = null
    private var pendingSubtitleStreamIndex: Int? = null
    private var pendingSubtitlesDisabled = false
    /** Global stream indexes required by the server's playback reporting API. */
    private var currentAudioStreamIndex: Int? = null
    private var currentSubtitleStreamIndex: Int? = null
    private var localPlaybackKey: String? = null
    private var legacyLocalPlaybackKey: String? = null
    private var lastLocalPositionPersistedAt = 0L

    init {
        castSessionManager?.addSessionManagerListener(castSessionListener, CastSession::class.java)
        updateCastState(castSessionManager?.currentCastSession != null)
        viewModelScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedSession && currentUserId != userId
                hasObservedSession = true
                currentUserId = userId
                if (userChanged) invalidatePlaybackForSessionChange()
            }
        }
        viewModelScope.launch {
            networkMonitor.isOnline.distinctUntilChanged().collect { isOnline ->
                val wasOffline = networkWasOffline
                networkWasOffline = !isOnline
                _state.update { it.copy(isNetworkOffline = !isOnline) }
                if (shouldRetryAfterNetworkRestored(
                        wasOffline = wasOffline,
                        isOnline = isOnline,
                        hasRemoteMedia = currentItemId != null,
                        hasPlaybackError = _state.value.error != null,
                        errorCode = lastPlaybackErrorCode,
                    )
                ) {
                    retryCurrentPlaybackAfterNetworkRestored()
                }
            }
        }
        viewModelScope.launch {
            settingsRepository.isAutoPlayEnabled().collect { autoPlayEnabled = it }
        }
        viewModelScope.launch {
            settingsRepository.isSkipIntroEnabled().collect { skipIntroEnabled = it }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultQuality().collect { defaultQuality = normalizeQualityPreference(it) }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultPlaybackSpeed().collect { defaultPlaybackSpeed = it }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultAspectRatio().collect { value ->
                defaultAspectRatio = normalizeAspectRatioPreference(value)
                _state.update { it.copy(aspectRatio = defaultAspectRatio) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getSubtitleFontSize().collect { size ->
                subtitleFontSize = size
                _state.update { it.copy(subtitleFontSize = size) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getSubtitleColor().collect { color ->
                subtitleColor = normalizeSubtitleColor(color)
                _state.update { it.copy(subtitleColor = subtitleColor) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isPiPEnabled().collect { enabled ->
                _state.update { it.copy(pictureInPictureEnabled = enabled) }
            }
        }
    }

    fun loadMedia(itemId: String) {
        val loadGeneration = ++playbackLoadGeneration
        val sessionAtLoad = sessionGeneration
        loadJob?.cancel()
        retryJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        sleepTimerJob?.cancel()
        sleepTimerJob = null
        player.stop()
        currentItemId = itemId
        localPlaybackKey = null
        legacyLocalPlaybackKey = null
        lastLocalPositionPersistedAt = 0L
        currentItemChapters = emptyList()
        currentItemSegments = emptyList()
        stoppedReported = false
        currentTranscodeUrl = null
        currentMediaMetadata = null
        lastPlaybackErrorCode = null
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
                isNetworkOffline = false,
                aspectRatio = defaultAspectRatio,
                subtitleColor = subtitleColor,
                sleepTimerRemainingMs = null,
                sleepTimerMinutes = null,
                sleepTimerMode = SleepTimerMode.OFF,
            )
        }
        loadJob = viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first()
            if (userId == null) {
                showLoadError("Faça login para reproduzir esta mídia.", loadGeneration)
                return@launch
            }
            localPlaybackKey = remotePlaybackPositionKey(userId, itemId)

            // Get playback info from server to determine best play method
            val item = getItemDetailUseCase(userId, itemId).getOrElse {
                showLoadError("Não foi possível carregar os dados desta mídia.", loadGeneration)
                return@launch
            }
            if (!isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) return@launch
            currentItemChapters = item.chapters
            _state.update { it.copy(title = item.name, chapters = item.chapters, error = null) }

            // Fetch Intro Skipper / native media segments
            viewModelScope.launch {
                playbackRepository.getMediaSegments(itemId).onSuccess { segments ->
                    if (isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) {
                        currentItemSegments = segments
                    }
                }
            }

            // Check for next episode in series
            viewModelScope.launch {
                getNextEpisodeUseCase(userId, item).onSuccess { next ->
                    if (next != null && isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) {
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
                showLoadError("O servidor não conseguiu preparar esta mídia.", loadGeneration)
                return@launch
            }
            val mediaSource = playbackInfo.mediaSources.firstOrNull() ?: run {
                showLoadError("Nenhuma fonte de reprodução está disponível para esta mídia.", loadGeneration)
                return@launch
            }
            val streamUrl = mediaSource.directStreamUrl
                ?: mediaSource.transcodeUrl
                ?: run {
                    showLoadError("O servidor não forneceu uma URL de reprodução.", loadGeneration)
                    return@launch
                }
            if (!isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) return@launch
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
                .mapIndexed { i, stream ->
                    TrackInfo(
                        index = stream.index,
                        displayName = stream.displayTitle ?: stream.displayLanguage ?: stream.language ?: "Legenda ${i + 1}",
                        language = stream.language ?: stream.displayLanguage,
                        codec = stream.codec,
                        isDefault = stream.isDefault,
                        isForced = stream.isForced,
                    )
                }

            val audioTracks = audioStreams
                .mapIndexed { i, stream ->
                    TrackInfo(
                        index = stream.index,
                        displayName = stream.displayTitle ?: stream.displayLanguage ?: stream.language ?: "Áudio ${i + 1}",
                        language = stream.language ?: stream.displayLanguage,
                        codec = stream.codec,
                        channels = stream.channels,
                        isDefault = stream.isDefault,
                    )
                }

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

            if (!isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) return@launch

            val videoStream = mediaStreams.firstOrNull { it.type == org.mulletaflix.domain.model.MediaStreamType.Video }
            val audioStream = mediaStreams.firstOrNull { it.type == org.mulletaflix.domain.model.MediaStreamType.Audio }
            val stats = PlaybackStats(
                videoCodec = videoStream?.codec?.uppercase(),
                audioCodec = audioStream?.codec?.uppercase(),
                resolution = if ((videoStream?.width ?: 0) > 0 && (videoStream?.height ?: 0) > 0) "${videoStream?.width}x${videoStream?.height}" else null,
                bitrate = videoStream?.bitRate?.let { "${it / 1000} kbps" },
                playMethod = if (mediaSource.transcodeUrl != null && streamUrl == mediaSource.transcodeUrl) "Transcode" else "Direct Play",
            )

            val availableQualities = qualityOptions(mediaStreams)
            _state.update {
                it.copy(
                    subtitleTracks = subtitleTracks,
                    audioTracks = audioTracks,
                    selectedSubtitleIndex = selectedSubtitleIndex,
                    selectedAudioIndex = selectedAudioIndex,
                    selectedQuality = effectiveQualitySelection(defaultQuality, availableQualities),
                    availableQualities = availableQualities,
                    showSkipIntro = false,
                    showSkipCredits = false,
                    skipTargetPosition = null,
                    playbackStats = stats,
                )
            }

            // Prepare Media3. CastPlayer automatically transfers this item when a
            // compatible Cast route is selected by the user.
            val artworkUri = resolveMediaUrl(
                baseUrl = sessionRepository.getBaseUrl().first(),
                path = item.primaryImageUrl,
                accessToken = sessionRepository.getAccessToken().first(),
            )?.let(Uri::parse)
            val mediaMetadata = MediaMetadata.Builder()
                .setTitle(mediaNotificationTitle(item))
                .setArtist(item.seriesName)
                .setDescription(item.overview)
                .setArtworkUri(artworkUri)
                .build()
            currentMediaMetadata = mediaMetadata
            val mediaItem = Media3Item.Builder()
                .setUri(streamUrl)
                .setMimeType(playbackMimeType(mediaSource.container, streamUrl))
                .setMediaMetadata(mediaMetadata)
                .build()

            if (!isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) return@launch

            player.setMediaItem(mediaItem)
            player.prepare()
            applyDefaultPlaybackPreferences()

            // Server progress is authoritative; local progress covers a temporary
            // sync delay after an interrupted remote session.
            val serverPositionMs = item.userProgress?.playbackPositionTicks?.div(10_000L)
            val localPositionMs = localPlaybackKey?.let { key ->
                offlinePlaybackPositions.getLong(key, 0L)
            }
            chooseResumePositionMs(serverPositionMs, localPositionMs)
                .takeIf { it > 0L }
                ?.let(player::seekTo)

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
        val loadGeneration = ++playbackLoadGeneration
        val sessionAtLoad = sessionGeneration
        loadJob?.cancel()
        retryJob?.cancel()
        progressJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        sleepTimerJob?.cancel()
        sleepTimerJob = null
        player.stop()
        currentItemId = null
        localPlaybackKey = null
        legacyLocalPlaybackKey = null
        viewModelScope.launch {
            val userId = resolveOfflinePlaybackUserId(
                cachedUserId = currentUserId,
                persistedUserId = sessionRepository.getCurrentUserId().first(),
            )
            if (userId == null) {
                _state.value = PlayerState(
                    title = title,
                    isBuffering = false,
                    error = "Faça login para reproduzir este download.",
                    isNetworkOffline = false,
                    aspectRatio = defaultAspectRatio,
                    subtitleColor = subtitleColor,
                )
                return@launch
            }
            if (loadGeneration != playbackLoadGeneration ||
                sessionAtLoad != sessionGeneration ||
                currentUserId?.let { it != userId } == true
            ) return@launch

            localPlaybackKey = offlinePlaybackPositionKey(userId, uri)
            legacyLocalPlaybackKey = offlinePlaybackPositionKey(uri)
            lastLocalPositionPersistedAt = 0L
            currentPlaySessionId = null
            currentMediaSourceId = null
            currentTranscodeUrl = null
            currentMediaMetadata = null
            lastPlaybackErrorCode = null
            currentAudioStreamIndex = null
            currentSubtitleStreamIndex = null
            triedTranscodeFallback = true
            playbackRetryCount = 0
            _state.value = PlayerState(
                title = title,
                isBuffering = true,
                error = null,
                isNetworkOffline = false,
                aspectRatio = defaultAspectRatio,
                subtitleColor = subtitleColor,
            )
            player.setMediaItem(
                Media3Item.Builder()
                    .setUri(uri)
                    .setMediaMetadata(
                        MediaMetadata.Builder()
                            .setTitle(title)
                            .build(),
                    )
                    .build(),
            )
            player.prepare()
            applyDefaultPlaybackPreferences()
            localOfflinePlaybackPosition()
                .takeIf { it > 0L }
                ?.let(player::seekTo)
            player.play()
        }
    }

    fun togglePlayPause() {
        if (player.isPlaying) player.pause() else player.play()
    }

    fun seekTo(positionMs: Long) {
        player.seekTo(positionMs)
        persistLocalPlaybackPosition(force = true)
        val generationAtReport = playbackLoadGeneration
        val itemIdAtReport = currentItemId
        val playSessionIdAtReport = currentPlaySessionId
        val mediaSourceIdAtReport = currentMediaSourceId
        val audioIndexAtReport = currentAudioStreamIndex
        val subtitleIndexAtReport = currentSubtitleStreamIndex
        val positionAtReport = positionMs.coerceAtLeast(0L)
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first() ?: return@launch
            if (isCurrentPlaybackReport(
                    expectedGeneration = generationAtReport,
                    currentGeneration = playbackLoadGeneration,
                    expectedItemId = itemIdAtReport,
                    currentItemId = currentItemId,
                    expectedPlaySessionId = playSessionIdAtReport,
                    currentPlaySessionId = currentPlaySessionId,
                    expectedMediaSourceId = mediaSourceIdAtReport,
                    currentMediaSourceId = currentMediaSourceId,
                ) && itemIdAtReport != null
            ) {
                playbackRepository.reportPlaybackProgress(
                    itemId = itemIdAtReport,
                    playSessionId = playSessionIdAtReport,
                    mediaSourceId = mediaSourceIdAtReport,
                    audioIndex = audioIndexAtReport,
                    subtitleIndex = subtitleIndexAtReport,
                    positionTicks = positionAtReport * 10_000L,
                    isPaused = !player.isPlaying,
                )
            }
        }
    }

    fun seekBy(deltaMs: Long) {
        seekTo(seekPositionByDelta(player.currentPosition, deltaMs, player.duration))
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
            viewModelScope.launch {
                settingsRepository.setPreferredSubtitleLanguage("off")
            }
            return
        }
        val track = _state.value.subtitleTracks.getOrNull(index) ?: return
        val serverIndex = track.index
        currentSubtitleStreamIndex = serverIndex
        selectTrackByServerIndex(serverIndex, C.TRACK_TYPE_TEXT)
        viewModelScope.launch {
            settingsRepository.setPreferredSubtitleLanguage(track.language)
        }
    }

    fun selectAudio(index: Int) {
        _state.update { it.copy(selectedAudioIndex = index) }
        val track = _state.value.audioTracks.getOrNull(index) ?: return
        val serverIndex = track.index
        currentAudioStreamIndex = serverIndex
        selectTrackByServerIndex(serverIndex, C.TRACK_TYPE_AUDIO)
        viewModelScope.launch {
            settingsRepository.setPreferredAudioLanguage(track.language)
        }
    }

    fun selectQuality(quality: String) {
        val normalizedQuality = normalizeQualityPreference(quality)
        applyQuality(normalizedQuality)
        viewModelScope.launch {
            settingsRepository.setDefaultQuality(normalizedQuality)
        }
    }

    private fun applyQuality(quality: String) {
        _state.update { it.copy(selectedQuality = quality) }
        val constraint = videoQualityConstraint(quality)
        localPlayer.trackSelectionParameters = localPlayer.trackSelectionParameters
            .buildUpon()
            .setMaxVideoSize(constraint.maxWidth, constraint.maxHeight)
            .setMaxVideoBitrate(constraint.maxBitrate)
            .build()
    }

    fun setPlaybackSpeed(speed: Float) {
        val normalizedSpeed = normalizePlaybackSpeed(speed)
        player.setPlaybackSpeed(normalizedSpeed)
        _state.update { it.copy(playbackSpeed = normalizedSpeed) }
        viewModelScope.launch {
            settingsRepository.setDefaultPlaybackSpeed(normalizedSpeed)
        }
    }

    fun setAspectRatio(ratio: VideoAspectRatio) {
        _state.update { it.copy(aspectRatio = ratio) }
        viewModelScope.launch {
            settingsRepository.setDefaultAspectRatio(ratio.name)
        }
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
        applyQuality(defaultQuality)
        setPlaybackSpeed(normalizePlaybackSpeed(defaultPlaybackSpeed))
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
                persistLocalPlaybackPosition()
                delay(250)
            }
        }
        serverProgressJob = viewModelScope.launch {
            while (isActive) {
                delay(5_000)
                val generationAtReport = playbackLoadGeneration
                val itemIdAtReport = currentItemId ?: continue
                val playSessionIdAtReport = currentPlaySessionId
                val mediaSourceIdAtReport = currentMediaSourceId
                val audioIndexAtReport = currentAudioStreamIndex
                val subtitleIndexAtReport = currentSubtitleStreamIndex
                val positionAtReport = player.currentPosition.coerceAtLeast(0L)
                val isPausedAtReport = isPlaybackPausedForReport(player.isPlaying)
                val reportSnapshot = PlaybackProgressSnapshot(
                    generation = generationAtReport,
                    itemId = itemIdAtReport,
                    playSessionId = playSessionIdAtReport,
                    mediaSourceId = mediaSourceIdAtReport,
                    audioIndex = audioIndexAtReport,
                    subtitleIndex = subtitleIndexAtReport,
                    positionMs = positionAtReport,
                    isPaused = isPausedAtReport,
                )
                val userId = sessionRepository.getCurrentUserId().first() ?: continue
                if (isCurrentPlaybackReport(
                        expectedGeneration = reportSnapshot.generation,
                        currentGeneration = playbackLoadGeneration,
                        expectedItemId = reportSnapshot.itemId,
                        currentItemId = currentItemId,
                        expectedPlaySessionId = reportSnapshot.playSessionId,
                        currentPlaySessionId = currentPlaySessionId,
                        expectedMediaSourceId = reportSnapshot.mediaSourceId,
                        currentMediaSourceId = currentMediaSourceId,
                    )
                ) {
                    playbackRepository.reportPlaybackProgress(
                        itemId = reportSnapshot.itemId!!,
                        playSessionId = reportSnapshot.playSessionId,
                        mediaSourceId = reportSnapshot.mediaSourceId,
                        audioIndex = reportSnapshot.audioIndex,
                        subtitleIndex = reportSnapshot.subtitleIndex,
                        positionTicks = reportSnapshot.positionMs * 10_000L,
                        isPaused = reportSnapshot.isPaused,
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
        persistLocalPlaybackPosition(force = isPaused)
        val generationAtReport = playbackLoadGeneration
        val itemIdAtReport = currentItemId
        val playSessionIdAtReport = currentPlaySessionId
        val mediaSourceIdAtReport = currentMediaSourceId
        val positionAtReport = player.currentPosition.coerceAtLeast(0L)
        val audioIndexAtReport = currentAudioStreamIndex
        val subtitleIndexAtReport = currentSubtitleStreamIndex
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first() ?: return@launch
            if (isCurrentPlaybackReport(
                    expectedGeneration = generationAtReport,
                    currentGeneration = playbackLoadGeneration,
                    expectedItemId = itemIdAtReport,
                    currentItemId = currentItemId,
                    expectedPlaySessionId = playSessionIdAtReport,
                    currentPlaySessionId = currentPlaySessionId,
                    expectedMediaSourceId = mediaSourceIdAtReport,
                    currentMediaSourceId = currentMediaSourceId,
                ) && itemIdAtReport != null
            ) {
                playbackRepository.reportPlaybackProgress(
                    itemId = itemIdAtReport,
                    playSessionId = playSessionIdAtReport,
                    mediaSourceId = mediaSourceIdAtReport,
                    positionTicks = positionAtReport * 10_000L,
                    audioIndex = audioIndexAtReport,
                    subtitleIndex = subtitleIndexAtReport,
                    isPaused = isPaused,
                )
            }
        }
    }

    private fun reportPlaybackStopped() {
        if (stoppedReported) return
        stoppedReported = true
        val generationAtReport = playbackLoadGeneration
        val itemIdAtReport = currentItemId
        val playSessionIdAtReport = currentPlaySessionId
        val mediaSourceIdAtReport = currentMediaSourceId
        val positionAtReport = player.currentPosition.coerceAtLeast(0L)
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first() ?: return@launch
            if (isCurrentPlaybackReport(
                    expectedGeneration = generationAtReport,
                    currentGeneration = playbackLoadGeneration,
                    expectedItemId = itemIdAtReport,
                    currentItemId = currentItemId,
                    expectedPlaySessionId = playSessionIdAtReport,
                    currentPlaySessionId = currentPlaySessionId,
                    expectedMediaSourceId = mediaSourceIdAtReport,
                    currentMediaSourceId = currentMediaSourceId,
                ) && itemIdAtReport != null
            ) {
                playbackRepository.reportPlaybackStopped(
                    itemId = itemIdAtReport,
                    playSessionId = playSessionIdAtReport,
                    mediaSourceId = mediaSourceIdAtReport,
                    positionTicks = positionAtReport * 10_000L,
                )
            }
        }
    }

    private var nextEpisodeCountdownJob: Job? = null
    private var sleepTimerJob: Job? = null

    fun setSleepTimer(minutes: Int?) {
        sleepTimerJob?.cancel()
        val normalizedMinutes = minutes?.let(::normalizeSleepTimerMinutes)
        if (normalizedMinutes == null) {
            _state.update {
                it.copy(
                    sleepTimerRemainingMs = null,
                    sleepTimerMinutes = null,
                    sleepTimerMode = SleepTimerMode.OFF,
                )
            }
            return
        }
        val durationMs = normalizedMinutes * 60_000L
        _state.update {
            it.copy(
                sleepTimerRemainingMs = durationMs,
                sleepTimerMinutes = normalizedMinutes,
                sleepTimerMode = SleepTimerMode.COUNTDOWN,
            )
        }
        sleepTimerJob = viewModelScope.launch {
            var remainingMs = durationMs
            while (remainingMs > 0L && isActive) {
                delay(1_000L)
                remainingMs = (remainingMs - 1_000L).coerceAtLeast(0L)
                _state.update { it.copy(sleepTimerRemainingMs = remainingMs) }
            }
            if (isActive) {
                player.pause()
                _state.update {
                    it.copy(
                        sleepTimerRemainingMs = null,
                        sleepTimerMinutes = null,
                        sleepTimerMode = SleepTimerMode.OFF,
                    )
                }
            }
        }
    }

    fun setSleepTimerAtMediaEnd() {
        sleepTimerJob?.cancel()
        sleepTimerJob = null
        _state.update {
            it.copy(
                sleepTimerRemainingMs = null,
                sleepTimerMinutes = null,
                sleepTimerMode = SleepTimerMode.AT_MEDIA_END,
            )
        }
    }

    fun cancelSleepTimer() {
        setSleepTimer(null)
    }

    private fun handlePlaybackEnded() {
        if (_state.value.sleepTimerMode == SleepTimerMode.AT_MEDIA_END) {
            _state.update { it.copy(sleepTimerMode = SleepTimerMode.OFF) }
            player.pause()
            return
        }
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
        castSessionManager?.removeSessionManagerListener(castSessionListener, CastSession::class.java)
        loadJob?.cancel()
        retryJob?.cancel()
        progressJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        sleepTimerJob?.cancel()
        persistLocalPlaybackPosition(force = true)
        reportPlaybackStopped()
        PlayerMediaSessionBridge.detach(mediaSession)
        player.release()
        localPlayer.release()
    }

    private fun updateCastState(isCasting: Boolean) {
        _state.update { it.copy(isCasting = isCasting) }
    }

    /** A session switch must stop the old media and invalidate every delayed callback. */
    private fun invalidatePlaybackForSessionChange() {
        sessionGeneration++
        playbackLoadGeneration++
        loadJob?.cancel()
        retryJob?.cancel()
        stopProgressReporting()
        nextEpisodeCountdownJob?.cancel()
        sleepTimerJob?.cancel()
        sleepTimerJob = null
        stoppedReported = true
        player.stop()
        currentItemId = null
        currentPlaySessionId = null
        currentMediaSourceId = null
        currentTranscodeUrl = null
        currentMediaMetadata = null
        localPlaybackKey = null
        legacyLocalPlaybackKey = null
        _state.update {
            it.copy(
                title = null,
                isPlaying = false,
                isBuffering = false,
                currentPosition = 0L,
                duration = 0L,
                nextEpisode = null,
                nextEpisodeCountdown = null,
                error = "A sessão foi alterada. Reabra a mídia para continuar.",
                chapters = emptyList(),
                currentChapterName = null,
                showSkipIntro = false,
                showSkipCredits = false,
                skipTargetPosition = null,
                playbackStats = null,
            )
        }
    }

    private fun persistLocalPlaybackPosition(force: Boolean = false) {
        val key = localPlaybackKey ?: return
        val now = android.os.SystemClock.elapsedRealtime()
        if (!force && now - lastLocalPositionPersistedAt < 5_000L) return
        val position = player.currentPosition.coerceAtLeast(0L)
        offlinePlaybackPositions.edit().putLong(key, position).apply()
        lastLocalPositionPersistedAt = now
    }

    /** Migrates one pre-account-isolation position into the active user's namespace. */
    private fun localOfflinePlaybackPosition(): Long {
        val scopedKey = localPlaybackKey ?: return 0L
        val scopedPosition = offlinePlaybackPositions.getLong(scopedKey, 0L)
        if (scopedPosition > 0L) return scopedPosition

        val legacyKey = legacyLocalPlaybackKey ?: return 0L
        val legacyPosition = offlinePlaybackPositions.getLong(legacyKey, 0L)
        if (legacyPosition > 0L) {
            offlinePlaybackPositions.edit()
                .remove(legacyKey)
                .putLong(scopedKey, legacyPosition)
                .apply()
        }
        return legacyPosition
    }

    private fun showLoadError(message: String, loadGeneration: Long? = null) {
        if (currentItemId != null &&
            (loadGeneration == null || loadGeneration == playbackLoadGeneration)
        ) {
            _state.update { it.copy(isBuffering = false, isPlaying = false, error = message) }
        }
    }

    private fun retryCurrentPlaybackAfterNetworkRestored() {
        val itemId = currentItemId ?: return
        if (retryJob?.isActive == true) return
        val positionAtError = localPlayer.currentPosition
        val generationAtRestore = playbackLoadGeneration
        retryJob = viewModelScope.launch {
            _state.update { it.copy(isBuffering = true, error = null) }
            delay(playbackRetryDelayMs(playbackRetryCount))
            if (isCurrentPlaybackLoad(
                    expectedGeneration = generationAtRestore,
                    currentGeneration = playbackLoadGeneration,
                    expectedItemId = itemId,
                    currentItemId = currentItemId,
                )
            ) {
                playbackRetryCount = 0
                player.prepare()
                player.seekTo(positionAtError)
                player.play()
            }
        }
    }

    private var currentItemChapters: List<Chapter> = emptyList()
    private var currentItemSegments: List<MediaSegment> = emptyList()
}
