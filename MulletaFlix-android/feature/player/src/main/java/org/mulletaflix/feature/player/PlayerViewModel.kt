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
import androidx.media3.exoplayer.DefaultLoadControl
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.exoplayer.trackselection.DefaultTrackSelector
import androidx.media3.datasource.DefaultDataSource
import androidx.media3.datasource.DefaultHttpDataSource
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import android.content.Context
import kotlinx.coroutines.CoroutineScope
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
import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.domain.repository.SyncPlayPlaybackStatus
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.OfflineDownloadCache
import org.mulletaflix.core.api.SyncPlayRealtimeClient
import org.mulletaflix.core.api.SyncPlayRealtimeEvent
import org.mulletaflix.core.common.dispatcher.ApplicationScope
import org.mulletaflix.domain.model.SUBTITLE_COLOR_WHITE
import org.mulletaflix.domain.model.normalizeSubtitleColor
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.domain.model.primaryImageUrl
import javax.inject.Inject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

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

enum class SyncPlayConnectionState {
    NONE,
    CONNECTED,
    RECONNECTING,
}

internal fun syncPlayConnectionMessage(state: SyncPlayConnectionState): String? = when (state) {
    SyncPlayConnectionState.RECONNECTING -> "SyncPlay desconectado — tentando reconectar…"
    SyncPlayConnectionState.NONE,
    SyncPlayConnectionState.CONNECTED,
    -> null
}

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
    /**
     * True once the user dismissed the next-episode prompt.
     *
     * Cancelling the countdown used to clear only `nextEpisodeCountdown`, while
     * the prompt's visibility also depends on playback having reached the end —
     * which stays true. The prompt therefore remained on screen and the cancel
     * button looked dead.
     */
    val nextEpisodePromptDismissed: Boolean = false,
    val error: String? = null,
    val aspectRatio: VideoAspectRatio = VideoAspectRatio.FIT,
    val isControlsLocked: Boolean = false,
    val estimatedEndTime: String? = null,
    val currentChapterName: String? = null,
    val chapters: List<Chapter> = emptyList(),
    val playbackStats: PlaybackStats? = null,
    val isNetworkOffline: Boolean = false,
    val isNetworkMetered: Boolean = false,
    val sleepTimerRemainingMs: Long? = null,
    val sleepTimerMinutes: Int? = null,
    val sleepTimerMode: SleepTimerMode = SleepTimerMode.OFF,
    val isCasting: Boolean = false,
    val syncPlayConnection: SyncPlayConnectionState = SyncPlayConnectionState.NONE,
)

/**
 * The state an offline playback session starts from.
 *
 * Built as a named factory so the settings the session must keep honouring are
 * spelled out. Constructing `PlayerState(...)` inline reset each of them to its
 * data-class default, so a downloaded item entered Picture-in-Picture even with
 * the setting switched off, lost the subtitle font size, and forgot that the
 * network was metered.
 */
internal fun offlinePlaybackState(
    title: String,
    isBuffering: Boolean,
    error: String?,
    aspectRatio: VideoAspectRatio,
    subtitleColor: String,
    subtitleFontSize: Int,
    pictureInPictureEnabled: Boolean,
    isNetworkMetered: Boolean,
    sleepTimer: SleepTimerSelection = SleepTimerSelection.Off,
): PlayerState = PlayerState(
    title = title,
    isBuffering = isBuffering,
    error = error,
    isNetworkOffline = false,
    aspectRatio = aspectRatio,
    subtitleColor = subtitleColor,
    subtitleFontSize = subtitleFontSize,
    pictureInPictureEnabled = pictureInPictureEnabled,
    isNetworkMetered = isNetworkMetered,
    sleepTimerRemainingMs = sleepTimer.remainingMs,
    sleepTimerMinutes = sleepTimer.minutes,
    sleepTimerMode = sleepTimer.mode,
)

@HiltViewModel
@UnstableApi
class PlayerViewModel @Inject constructor(
    @ApplicationContext context: Context,
    private val getItemDetailUseCase: GetItemDetailUseCase,
    private val playbackRepository: PlaybackRepository,
    private val sessionRepository: SessionRepository,
    private val syncPlayRealtimeClient: SyncPlayRealtimeClient,
    private val manageSyncPlayUseCase: ManageSyncPlayUseCase,
    private val settingsRepository: SettingsRepository,
    private val getNextEpisodeUseCase: GetNextEpisodeUseCase,
    private val networkMonitor: NetworkMonitor,
    @param:ApplicationScope private val teardownScope: CoroutineScope,
) : ViewModel() {

    private val offlinePlaybackPositions = context.getSharedPreferences(
        "offline_playback_positions",
        Context.MODE_PRIVATE,
    )

    private val _state = MutableStateFlow(PlayerState())
    val state: StateFlow<PlayerState> = _state.asStateFlow()

    private val trackSelector = DefaultTrackSelector(context)
    private val streamingPolicy = streamingBufferPolicy()
    private val localPolicy = localBufferPolicy()
    private val localPlayer: ExoPlayer = ExoPlayer.Builder(context)
        .setTrackSelector(trackSelector)
        .setLoadControl(
            DefaultLoadControl.Builder()
                .setBufferDurationsMsForStreaming(
                    streamingPolicy.minBufferMs,
                    streamingPolicy.maxBufferMs,
                    streamingPolicy.bufferForPlaybackMs,
                    streamingPolicy.bufferForPlaybackAfterRebufferMs,
                )
                .setBufferDurationsMsForLocalPlayback(
                    localPolicy.minBufferMs,
                    localPolicy.maxBufferMs,
                    localPolicy.bufferForPlaybackMs,
                    localPolicy.bufferForPlaybackAfterRebufferMs,
                )
                .build(),
        )
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
                playbackCacheDataSourceFactory(
                    cache = OfflineDownloadCache.get(context),
                    upstreamFactory = DefaultHttpDataSource.Factory()
                        .setConnectTimeoutMs(MEDIA_CONNECT_TIMEOUT_MS)
                        .setReadTimeoutMs(MEDIA_READ_TIMEOUT_MS)
                        .setAllowCrossProtocolRedirects(true),
                ),
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
                if (playbackState == Player.STATE_BUFFERING || playbackState == Player.STATE_READY) {
                    enqueueSyncPlayPlaybackStatus(playbackState == Player.STATE_BUFFERING)
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
                // A download has no server metadata, so its track lists can only
                // come from the container. Without this the OSD's audio and
                // subtitle buttons were permanently disabled offline, because
                // they are enabled from these lists.
                if (isOfflinePlayback) refreshOfflineTracks(tracks)
                if (pendingAudioStreamIndex != null) {
                    val index = pendingAudioStreamIndex!!
                    selectTrackByServerIndex(index, C.TRACK_TYPE_AUDIO)
                    pendingAudioStreamIndex = null
                } else if (restoreTrackSelectionOnNextTracksChange) {
                    playbackAudioStreamIndex?.let { index ->
                        selectTrackByServerIndex(index, C.TRACK_TYPE_AUDIO)
                    }
                }
                if (pendingSubtitlesDisabled) {
                    selectTrackByServerIndex(-1, C.TRACK_TYPE_TEXT)
                    pendingSubtitlesDisabled = false
                } else if (pendingSubtitleStreamIndex != null) {
                    selectTrackByServerIndex(pendingSubtitleStreamIndex!!, C.TRACK_TYPE_TEXT)
                    pendingSubtitleStreamIndex = null
                } else if (restoreTrackSelectionOnNextTracksChange) {
                    if (playbackSubtitlesDisabled) {
                        selectTrackByServerIndex(-1, C.TRACK_TYPE_TEXT)
                    } else {
                        playbackSubtitleStreamIndex?.let { index ->
                            selectTrackByServerIndex(index, C.TRACK_TYPE_TEXT)
                        }
                    }
                }
                if (restoreTrackSelectionOnNextTracksChange) {
                    restoreTrackSelectionOnNextTracksChange = false
                }
            }

            override fun onPlayerError(error: PlaybackException) {
                lastPlaybackErrorCode = error.errorCode
                lastPlaybackPositionAtError = localPlayer.currentPosition.coerceAtLeast(0L)
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
                    restoreTrackSelectionOnNextTracksChange = true
                    player.prepare()
                    player.seekTo(positionAtError)
                    player.play()
                    return
                }
                val itemIdAtError = currentItemId
                val uriAtError = localPlayer.currentMediaItem?.localConfiguration?.uri
                if (itemIdAtError != null && shouldRetryPlayback(error.errorCode, playbackRetryCount)) {
                    if (shouldPausePlaybackForOffline(
                            isOnline = !networkWasOffline,
                            isOfflinePlayback = isOfflinePlayback,
                            hasRemoteMedia = true,
                            errorCode = error.errorCode,
                        )
                    ) {
                        _state.update {
                            it.copy(
                                isBuffering = false,
                                isPlaying = false,
                                error = NETWORK_WAITING_PLAYBACK_MESSAGE,
                            )
                        }
                        return
                    }
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
                            restoreTrackSelectionOnNextTracksChange = true
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
    private val syncPlayCommandScheduler = LatestSyncPlayCommandScheduler(viewModelScope)
    private var currentItemId: String? = null
    private var currentPlaylistItemId: String? = null
    private var pendingSyncPositionMs: Long? = null
    private var pendingSyncIsPlaying: Boolean? = null
    private var currentPlaySessionId: String? = null
    private var currentMediaSourceId: String? = null
    private var currentTranscodeUrl: String? = null
    private var currentMediaMetadata: MediaMetadata? = null
    private var triedTranscodeFallback = false
    private var playbackRetryCount = 0
    private var isOfflinePlayback = false
    private var playbackLoadGeneration = 0L
    private var currentUserId: String? = null
    private var hasObservedSession = false
    private var sessionGeneration = 0L
    private var networkWasOffline = false
    private var lastPlaybackErrorCode: Int? = null
    private var lastPlaybackPositionAtError = 0L
    private var stoppedReported = false
    private val syncPlayStatusProcessor by lazy(LazyThreadSafetyMode.NONE) {
        SyncPlayStatusEventProcessor(
            scope = viewModelScope,
            currentSnapshot = {
                SyncPlayStatusSnapshot(
                    generation = syncPlayRealtimeClient.connectionState.value.generation,
                    groupId = syncPlayRealtimeClient.activeGroupId,
                    playlistItemId = currentPlaylistItemId,
                    isOfflinePlayback = isOfflinePlayback,
                    networkIsOffline = networkWasOffline,
                    realtimeConnected = syncPlayRealtimeClient.connectionState.value.connected,
                    playerIsBuffering = player.playbackState == Player.STATE_BUFFERING,
                )
            },
        ) { event ->
            val status = SyncPlayPlaybackStatus(
                whenUtc = syncPlayWhenUtcNow(),
                positionTicks = player.currentPosition.coerceAtLeast(0L) * 10_000L,
                isPlaying = player.playWhenReady,
                playlistItemId = event.playlistItemId,
            )
            val result = if (event.isBuffering) {
                manageSyncPlayUseCase.reportBuffering(status)
            } else {
                manageSyncPlayUseCase.reportReady(status)
            }
            result.isSuccess
        }
    }
    private var lastPlaybackStopJob: Job? = null
    private var autoPlayEnabled = true
    private var skipIntroEnabled = true
    private var defaultQuality = "Auto"
    private var networkIsMetered = false
    private var defaultPlaybackSpeed = 1f
    private var defaultAspectRatio = VideoAspectRatio.FIT
    private var subtitleFontSize = 100
    private var subtitleColor = SUBTITLE_COLOR_WHITE
    private var pendingAudioStreamIndex: Int? = null
    private var pendingSubtitleStreamIndex: Int? = null
    private var pendingSubtitlesDisabled = false
    /** Keeps a user's manual track choice while Media3 rebuilds tracks after a retry. */
    private var playbackAudioStreamIndex: Int? = null
    private var playbackSubtitleStreamIndex: Int? = null
    private var playbackSubtitlesDisabled = false
    private var restoreTrackSelectionOnNextTracksChange = false
    /** Global stream indexes required by the server's playback reporting API. */
    private var currentAudioStreamIndex: Int? = null
    private var currentSubtitleStreamIndex: Int? = null

    /**
     * Server stream indices of each type, in the order the container lists them.
     *
     * `selectTrackByServerIndex` needs the position of a server index among its
     * own type; the container's track id is a different numbering space.
     */
    private var currentAudioStreamIndices: List<Int> = emptyList()
    private var currentSubtitleStreamIndices: List<Int> = emptyList()
    private var localPlaybackKey: String? = null
    private var legacyLocalPlaybackKey: String? = null
    private var lastLocalPositionPersistedAt = 0L

    init {
        viewModelScope.launch {
            syncPlayRealtimeClient.connectionState.collect { connection ->
                if (syncPlayRealtimeClient.activeGroupId == null) return@collect
                if (connection.connected) {
                    _state.update { it.copy(syncPlayConnection = SyncPlayConnectionState.CONNECTED) }
                    enqueueSyncPlayPlaybackStatus(player.playbackState == Player.STATE_BUFFERING)
                } else {
                    _state.update { it.copy(syncPlayConnection = SyncPlayConnectionState.RECONNECTING) }
                }
            }
        }
        viewModelScope.launch {
            syncPlayRealtimeClient.events.collect { event ->
                when (event) {
                    SyncPlayRealtimeEvent.Connected,
                    SyncPlayRealtimeEvent.Disconnected -> Unit
                    is SyncPlayRealtimeEvent.Command -> applySyncPlayCommand(event)
                    is SyncPlayRealtimeEvent.QueueUpdate -> applySyncPlayQueueUpdate(event)
                    is SyncPlayRealtimeEvent.GroupUpdate -> Unit
                }
            }
        }
        castSessionManager?.addSessionManagerListener(castSessionListener, CastSession::class.java)
        updateCastState(castSessionManager?.currentCastSession != null)
        viewModelScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedSession && currentUserId != userId
                hasObservedSession = true
                val previousUserId = currentUserId
                if (userChanged) invalidatePlaybackForSessionChange(previousUserId)
                currentUserId = userId
            }
        }
        viewModelScope.launch {
            // The app moves between the LAN address and the public one on its own when
            // the network disappears, and a prepared stream keeps the address it was
            // built with. Without this the episode dies the moment the user walks out
            // of Wi-Fi range and only returns by reopening the title.
            sessionRepository.getBaseUrl().distinctUntilChanged().collect { baseUrl ->
                preparedStreamRetarget.onBaseUrlChanged(baseUrl)
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
                if (!isOnline && !isOfflinePlayback && currentItemId != null && retryJob?.isActive == true) {
                    retryJob?.cancel()
                    retryJob = null
                    _state.update {
                        it.copy(
                            isBuffering = false,
                            isPlaying = false,
                            error = NETWORK_WAITING_PLAYBACK_MESSAGE,
                        )
                    }
                }
            }
        }
        viewModelScope.launch {
            networkMonitor.isMetered.distinctUntilChanged().collect { isMetered ->
                networkIsMetered = isMetered
                _state.update { it.copy(isNetworkMetered = isMetered) }
                if (defaultQuality == "Auto") applyQuality("Auto")
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
        val previousStopJob = stopCurrentRemotePlaybackBeforeLoad()
        val loadGeneration = ++playbackLoadGeneration
        val sessionAtLoad = sessionGeneration
        loadJob?.cancel()
        retryJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        // O timer de sono não é cancelado aqui. Ele é da sessão, não do item, e
        // esta função também é o caminho do avanço automático: cancelá-lo fazia
        // "pausar em 30 minutos" morrer no primeiro episódio seguinte, sempre.
        player.stop()
        currentItemId = itemId
        currentPlaylistItemId = null
        pendingSyncPositionMs = null
        pendingSyncIsPlaying = null
        isOfflinePlayback = false
        // The stop report captured the previous session above. Clear its
        // identity before a rapid second navigation can mistake it for the new
        // item while that item's PlaybackInfo is still in flight.
        currentPlaySessionId = null
        currentMediaSourceId = null
        localPlaybackKey = null
        legacyLocalPlaybackKey = null
        lastLocalPositionPersistedAt = 0L
        currentItemChapters = emptyList()
        currentItemSegments = emptyList()
        stoppedReported = false
        currentTranscodeUrl = null
        currentMediaMetadata = null
        lastPlaybackErrorCode = null
        lastPlaybackPositionAtError = 0L
        currentAudioStreamIndex = null
        currentSubtitleStreamIndex = null
        playbackAudioStreamIndex = null
        playbackSubtitleStreamIndex = null
        playbackSubtitlesDisabled = false
        restoreTrackSelectionOnNextTracksChange = false
        triedTranscodeFallback = false
        playbackRetryCount = 0
        _state.update {
            it.forNewItem(
                aspectRatio = defaultAspectRatio,
                subtitleColor = subtitleColor,
            )
        }
        loadJob = viewModelScope.launch {
            previousStopJob?.join()
            if (!isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) return@launch
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
                // Uma falha aqui não é "a série acabou": o aviso de "Próximo
                // episódio" some em `null`, então uma falha transitória precisa de
                // outra chance antes de a maratona terminar em silêncio.
                //
                // A insistência em si mora em `settleNextEpisodeLookup`, que é testável;
                // aqui ficou só o que é efeito — publicar o episódio encontrado, e só se
                // esta reprodução ainda for a atual.
                val result = settleNextEpisodeLookup { getNextEpisodeUseCase(userId, item) }
                val next = result.getOrNull() ?: return@launch
                if (isCurrentPlaybackLoad(loadGeneration, playbackLoadGeneration, itemId, currentItemId, sessionAtLoad, sessionGeneration)) {
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

            val preferredAudioLanguage = settingsRepository.getPreferredAudioLanguage().first()
            val preferredSubtitleLanguage = settingsRepository.getPreferredSubtitleLanguage().first()
            val itemAudioStreams = item.mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Audio }
            val itemSubtitleStreams = item.mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Subtitle }
            val requestedAudioStreamIndex = requestedPreferredStreamIndex(
                streams = itemAudioStreams,
                preferredLanguage = preferredAudioLanguage,
            )
            val requestedSubtitleStreamIndex = requestedPreferredStreamIndex(
                streams = itemSubtitleStreams,
                preferredLanguage = preferredSubtitleLanguage,
            )

            val playbackInfo = playbackRepository.getPlaybackInfo(
                itemId = itemId,
                userId = userId,
                audioStreamIndex = requestedAudioStreamIndex,
                subtitleStreamIndex = requestedSubtitleStreamIndex,
                startTimeTicks = item.userProgress?.playbackPositionTicks,
            ).getOrElse {
                showLoadError("O servidor não conseguiu preparar esta mídia.", loadGeneration)
                return@launch
            }
            val mediaSource = playbackInfo.mediaSources.firstOrNull() ?: run {
                // A frase vem do `PlaybackInfo`: quando o servidor recusou preparar,
                // ele disse o motivo (`ErrorCode`) e a mensagem específica está lá.
                showLoadError(playbackInfo.unavailableMessage, loadGeneration)
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
            val recoverySelection = trackRecoverySelection(
                audioStreamIndex = preferredAudioStreamIndex,
                subtitleStreamIndex = preferredSubtitleStreamIndex,
                subtitlesDisabled = preferredSubtitleLanguage.equals("off", ignoreCase = true) ||
                    preferredSubtitleLanguage.equals("none", ignoreCase = true),
            )
            playbackAudioStreamIndex = recoverySelection.audioStreamIndex
            playbackSubtitleStreamIndex = recoverySelection.subtitleStreamIndex
            playbackSubtitlesDisabled = recoverySelection.subtitlesDisabled
            pendingSubtitlesDisabled = playbackSubtitlesDisabled

            // Server indices of each type, in container order. Selection maps a
            // server index to its position here, because the container's own
            // track id lives in a different numbering space (see
            // `trackCandidatePosition`).
            currentAudioStreamIndices = orderedStreamIndices(audioStreams.map { it.index })
            currentSubtitleStreamIndices = orderedStreamIndices(subtitleStreams.map { it.index })

            val subtitleTracks = subtitleStreams
                .mapIndexed { i, stream ->
                    TrackInfo(
                        index = stream.index,
                        displayName = friendlyTrackName(
                            displayName = stream.displayTitle,
                            language = stream.displayLanguage ?: stream.language,
                            fallback = "Legenda ${i + 1}",
                        ),
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
                        displayName = friendlyTrackName(
                            displayName = stream.displayTitle,
                            language = stream.displayLanguage ?: stream.language,
                            fallback = "Áudio ${i + 1}",
                        ),
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
                .setMediaId(itemId)
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

            val syncPositionMs = pendingSyncPositionMs
            val syncIsPlaying = pendingSyncIsPlaying
            if (syncPositionMs != null) player.seekTo(syncPositionMs)
            pendingSyncPositionMs = null
            pendingSyncIsPlaying = null
            when (syncIsPlaying) {
                true -> player.play()
                false -> player.pause()
                null -> if (autoPlayEnabled) player.play()
            }

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
        val previousStopJob = stopCurrentRemotePlaybackBeforeLoad()
        val loadGeneration = ++playbackLoadGeneration
        val sessionAtLoad = sessionGeneration
        loadJob?.cancel()
        retryJob?.cancel()
        progressJob?.cancel()
        serverProgressJob?.cancel()
        nextEpisodeCountdownJob?.cancel()
        // Como em `loadMedia`: o timer pertence à sessão e sobrevive à troca de
        // item, inclusive para um download.
        player.stop()
        currentItemId = null
        currentPlaylistItemId = null
        pendingSyncPositionMs = null
        pendingSyncIsPlaying = null
        isOfflinePlayback = true
        currentPlaySessionId = null
        currentMediaSourceId = null
        localPlaybackKey = null
        legacyLocalPlaybackKey = null
        viewModelScope.launch {
            previousStopJob?.join()
            val userId = resolveOfflinePlaybackUserId(
                cachedUserId = currentUserId,
                persistedUserId = sessionRepository.getCurrentUserId().first(),
            )
            if (userId == null) {
                if (loadGeneration != playbackLoadGeneration || sessionAtLoad != sessionGeneration) return@launch
                _state.value = offlinePlaybackState(
                    title = title,
                    isBuffering = false,
                    error = "Faça login para reproduzir este download.",
                    aspectRatio = defaultAspectRatio,
                    subtitleColor = subtitleColor,
                    subtitleFontSize = _state.value.subtitleFontSize,
                    pictureInPictureEnabled = _state.value.pictureInPictureEnabled,
                    isNetworkMetered = _state.value.isNetworkMetered,
                    sleepTimer = _state.value.sleepTimerSelection(),
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
            lastPlaybackPositionAtError = 0L
            currentAudioStreamIndex = null
            currentSubtitleStreamIndex = null
            triedTranscodeFallback = true
            playbackRetryCount = 0
            _state.value = offlinePlaybackState(
                title = title,
                isBuffering = true,
                error = null,
                aspectRatio = defaultAspectRatio,
                subtitleColor = subtitleColor,
                subtitleFontSize = _state.value.subtitleFontSize,
                pictureInPictureEnabled = _state.value.pictureInPictureEnabled,
                isNetworkMetered = _state.value.isNetworkMetered,
                sleepTimer = _state.value.sleepTimerSelection(),
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

    private fun applySyncPlayQueueUpdate(event: SyncPlayRealtimeEvent.QueueUpdate) {
        if (syncPlayRealtimeClient.activeGroupId != event.groupId) return
        val itemId = event.itemId ?: return
        syncPlayCommandScheduler.cancelPending()
        currentPlaylistItemId = event.playlistItemId
        val positionMs = syncPlayPositionMs(event.startPositionTicks) ?: 0L
        if (currentItemId != itemId) {
            loadMedia(itemId)
            currentPlaylistItemId = event.playlistItemId
            pendingSyncPositionMs = positionMs
            pendingSyncIsPlaying = event.isPlaying
            return
        }
        syncPlayQueueCorrectionPositionMs(
            authoritativePositionMs = positionMs,
            currentPositionMs = player.currentPosition,
        )?.let(player::seekTo)
        if (event.isPlaying) player.play() else player.pause()
        enqueueSyncPlayPlaybackStatus(player.playbackState == Player.STATE_BUFFERING)
    }

    private fun enqueueSyncPlayPlaybackStatus(isBuffering: Boolean) {
        val groupId = syncPlayRealtimeClient.activeGroupId
        val playlistItemId = currentPlaylistItemId
        if (isOfflinePlayback || networkWasOffline || groupId.isNullOrBlank() || playlistItemId.isNullOrBlank()) {
            return
        }
        if (currentItemId == null) return
        val connection = syncPlayRealtimeClient.connectionState.value
        if (!connection.connected) return
        syncPlayStatusProcessor.enqueue(
            SyncPlayStatusEvent(
                generation = connection.generation,
                groupId = groupId,
                playlistItemId = playlistItemId,
                isBuffering = isBuffering,
            ),
        )
    }

    private fun syncPlayWhenUtcNow(): String = SimpleDateFormat(
        "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
        Locale.US,
    ).apply {
        timeZone = TimeZone.getTimeZone("UTC")
    }.format(Date())

    private fun applySyncPlayCommand(event: SyncPlayRealtimeEvent.Command) {
        if (!shouldApplySyncPlayCommand(
                event = event,
                activeGroupId = syncPlayRealtimeClient.activeGroupId,
                currentItemId = currentItemId,
                currentPlaylistItemId = currentPlaylistItemId,
            )
        ) return

        val delayMs = ((event.whenEpochMs ?: 0L) - System.currentTimeMillis())
            .coerceAtLeast(0L)
            .coerceAtMost(30_000L)
        val generationAtCommand = playbackLoadGeneration
        syncPlayCommandScheduler.schedule(delayMs) {
            if (generationAtCommand != playbackLoadGeneration ||
                !shouldApplySyncPlayCommand(
                    event = event,
                    activeGroupId = syncPlayRealtimeClient.activeGroupId,
                    currentItemId = currentItemId,
                    currentPlaylistItemId = currentPlaylistItemId,
                )
            ) return@schedule
            when (event.command.lowercase()) {
                "pause" -> {
                    syncPlayCommandPositionMs(event.command, event.positionTicks)?.let(player::seekTo)
                    player.pause()
                }
                "unpause" -> {
                    syncPlayCommandPositionMs(event.command, event.positionTicks)?.let(player::seekTo)
                    player.play()
                }
                "stop" -> player.stop()
                "seek" -> syncPlayCommandPositionMs(event.command, event.positionTicks)?.let(player::seekTo)
            }
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

    /** Retries the already prepared remote item without discarding its position. */
    fun retryPlayback() {
        // A decisão é uma função pura e testada (PlaybackRetryPlan); aqui só ficam
        // os efeitos, que são a parte que nenhum teste alcança.
        val itemId = currentItemId
        when (
            val plan = playbackRetryPlan(
                itemId = itemId,
                isOfflinePlayback = isOfflinePlayback,
                isNetworkOffline = networkWasOffline,
                hasPreparedMedia = localPlayer.currentMediaItem != null,
                currentPositionMs = localPlayer.currentPosition,
                positionAtErrorMs = lastPlaybackPositionAtError,
            )
        ) {
            PlaybackRetryPlan.Nothing -> return

            PlaybackRetryPlan.WarnOffline -> {
                // Returning silently made "Tentar novamente" look broken during an
                // outage. The network collector already retries by itself once the
                // connection is back, so say that instead of doing nothing.
                _state.update {
                    it.copy(
                        isBuffering = false,
                        error = "Você está offline. A reprodução reinicia sozinha quando a conexão voltar.",
                    )
                }
                return
            }

            PlaybackRetryPlan.Reload -> {
                // `Reload` só é escolhido com itemId não nulo.
                loadMedia(itemId ?: return)
                return
            }

            is PlaybackRetryPlan.Restart -> {
                val retryItemId = itemId ?: return
                retryJob?.cancel()
                val generation = playbackLoadGeneration
                val position = plan.positionMs
                playbackRetryCount = 0
                _state.update { it.copy(isBuffering = true, error = null, isPlaying = false) }
                retryJob = viewModelScope.launch {
                    if (!isCurrentPlaybackLoad(generation, playbackLoadGeneration, retryItemId, currentItemId)) return@launch
                    restoreTrackSelectionOnNextTracksChange = true
                    player.prepare()
                    player.seekTo(position)
                    player.play()
                }
            }
        }
    }

    /**
     * Re-points the stream that is already prepared when the server address changes.
     *
     * The steps and their order live in [PreparedStreamRetarget]; this is the adapter
     * onto the real player, kept as thin as it can be because it is the part no test
     * can reach.
     */
    private val preparedStreamRetarget = PreparedStreamRetarget(
        stream = object : RetargetableStream {
            override fun preparedUrl(): String? =
                localPlayer.currentMediaItem?.localConfiguration?.uri?.toString()

            override fun isOffline(): Boolean = isOfflinePlayback

            override fun positionMs(): Long = localPlayer.currentPosition

            override fun isPlaying(): Boolean = localPlayer.playWhenReady

            override fun replaceSource(url: String, positionMs: Long, resumePlayback: Boolean) {
                val item = localPlayer.currentMediaItem ?: return
                // A new media source drops the selected audio and subtitle tracks, so
                // the same flag the error retry uses to restore them is set here.
                restoreTrackSelectionOnNextTracksChange = true
                localPlayer.setMediaItem(item.buildUpon().setUri(url).build(), positionMs)
                localPlayer.prepare()
                if (resumePlayback) localPlayer.play()
            }
        },
        accessToken = { sessionRepository.getAccessToken().first() },
    )

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
            playbackSubtitleStreamIndex = null
            playbackSubtitlesDisabled = true
            selectTrackByServerIndex(-1, C.TRACK_TYPE_TEXT)
            viewModelScope.launch {
                settingsRepository.setPreferredSubtitleLanguage("off")
            }
            return
        }
        val track = _state.value.subtitleTracks.getOrNull(index) ?: return
        val serverIndex = track.index
        currentSubtitleStreamIndex = serverIndex
        playbackSubtitleStreamIndex = serverIndex
        playbackSubtitlesDisabled = false
        selectTrackByServerIndex(serverIndex, C.TRACK_TYPE_TEXT)
        viewModelScope.launch {
            persistableTrackLanguage(track.language)?.let {
                settingsRepository.setPreferredSubtitleLanguage(it)
            }
        }
    }

    fun selectAudio(index: Int) {
        _state.update { it.copy(selectedAudioIndex = index) }
        val track = _state.value.audioTracks.getOrNull(index) ?: return
        val serverIndex = track.index
        currentAudioStreamIndex = serverIndex
        playbackAudioStreamIndex = serverIndex
        selectTrackByServerIndex(serverIndex, C.TRACK_TYPE_AUDIO)
        viewModelScope.launch {
            persistableTrackLanguage(track.language)?.let {
                settingsRepository.setPreferredAudioLanguage(it)
            }
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
        val normalizedQuality = normalizeQualityPreference(quality)
        // The stored preference may name a resolution this title does not offer.
        // Writing it raw made the state claim "4K" while the menu — built from
        // `qualityMenuOptions(availableQualities)` — showed no row selected at
        // all, so the control lied about what was playing. `effectiveQualitySelection`
        // answers "Auto" in that case, which is a value the menu does offer.
        _state.update {
            it.copy(
                selectedQuality = appliedQualitySelection(normalizedQuality, it.availableQualities),
            )
        }
        val constraint = videoQualityConstraint(effectivePlaybackQuality(normalizedQuality, networkIsMetered))
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

    /**
     * Rebuilds the audio and subtitle lists from the container while playing a
     * download, and points the position lookup at their own indices.
     */
    private fun refreshOfflineTracks(tracks: androidx.media3.common.Tracks) {
        fun collect(trackType: Int): List<OfflineTrack> = buildList {
            tracks.groups
                .filter { it.type == trackType }
                .forEach { group ->
                    for (trackIndex in 0 until group.length) {
                        if (!group.isTrackSupported(trackIndex)) continue
                        val format = group.getTrackFormat(trackIndex)
                        add(
                            OfflineTrack(
                                language = format.language,
                                codec = format.sampleMimeType,
                                channels = format.channelCount.takeIf { it > 0 },
                                isDefault = format.selectionFlags and C.SELECTION_FLAG_DEFAULT != 0,
                                isForced = format.selectionFlags and C.SELECTION_FLAG_FORCED != 0,
                                isSelected = group.isTrackSelected(trackIndex),
                            ),
                        )
                    }
                }
        }

        val offlineAudio = collect(C.TRACK_TYPE_AUDIO)
        val offlineSubtitles = collect(C.TRACK_TYPE_TEXT)
        if (offlineAudio.isEmpty() && offlineSubtitles.isEmpty()) return

        val audio = offlineTrackInfos(offlineAudio, "Áudio")
        val subtitles = offlineTrackInfos(offlineSubtitles, "Legenda")

        currentAudioStreamIndices = offlineStreamIndices(audio.size)
        currentSubtitleStreamIndices = offlineStreamIndices(subtitles.size)
        _state.update {
            it.copy(
                audioTracks = audio,
                subtitleTracks = subtitles,
                // Mark what is actually playing, and keep an explicit "no
                // subtitles" choice from reverting to the container's default.
                selectedAudioIndex = selectedOfflineTrackIndex(offlineAudio),
                selectedSubtitleIndex = if (it.selectedSubtitleIndex < 0) {
                    selectedOfflineTrackIndex(offlineSubtitles)
                } else {
                    it.selectedSubtitleIndex
                },
            )
        }
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

        val orderedIndices = when (trackType) {
            C.TRACK_TYPE_AUDIO -> currentAudioStreamIndices
            C.TRACK_TYPE_TEXT -> currentSubtitleStreamIndices
            else -> emptyList()
        }
        // The server index is not a container track id and not a track index
        // inside a group: it is resolved by its position among the streams of the
        // same type, which the container preserves.
        val position = trackCandidatePosition(index, orderedIndices) ?: return

        val candidates = player.currentTracks.groups
            .filter { it.type == trackType }
            .flatMap { group ->
                (0 until group.length).mapNotNull { trackIndex ->
                    if (group.isTrackSupported(trackIndex)) group to trackIndex else null
                }
            }
        val selected = candidates.getOrNull(position) ?: return
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
                    sessionGeneration = sessionGeneration,
                    userId = currentUserId ?: sessionRepository.getCurrentUserId().first(),
                    itemId = itemIdAtReport,
                    playSessionId = playSessionIdAtReport,
                    mediaSourceId = mediaSourceIdAtReport,
                    audioIndex = audioIndexAtReport,
                    subtitleIndex = subtitleIndexAtReport,
                    positionMs = positionAtReport,
                    isPaused = isPausedAtReport,
                )
                val userId = reportSnapshot.userId ?: continue
                if (isCurrentPlaybackReport(
                        expectedGeneration = reportSnapshot.generation,
                        currentGeneration = playbackLoadGeneration,
                        expectedItemId = reportSnapshot.itemId,
                        currentItemId = currentItemId,
                        expectedPlaySessionId = reportSnapshot.playSessionId,
                        currentPlaySessionId = currentPlaySessionId,
                        expectedMediaSourceId = reportSnapshot.mediaSourceId,
                        currentMediaSourceId = currentMediaSourceId,
                        expectedSessionGeneration = reportSnapshot.sessionGeneration,
                        currentSessionGeneration = sessionGeneration,
                        expectedUserId = reportSnapshot.userId,
                        currentUserId = currentUserId,
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

    private fun stopCurrentRemotePlaybackBeforeLoad(): Job? {
        if (shouldReportRemotePlaybackBeforeLoad(
                currentItemId = currentItemId,
                isOfflinePlayback = isOfflinePlayback,
                playSessionId = currentPlaySessionId,
                mediaSourceId = currentMediaSourceId,
                stoppedReported = stoppedReported,
            )
        ) {
            // The next load intentionally changes generation and item identity;
            // allow this snapshot to reach the server after that transition.
            return reportPlaybackStopped(allowPlaybackChange = true)
        }
        // Ended playback already reported its stop, but the next episode must
        // still wait for that request before it starts a new server session.
        return lastPlaybackStopJob?.takeUnless { it.isCompleted }
    }

    private fun reportPlaybackStopped(
        allowPlaybackChange: Boolean = false,
        userIdOverride: String? = currentUserId,
    ): Job? {
        if (stoppedReported) return lastPlaybackStopJob
        stoppedReported = true
        val generationAtReport = playbackLoadGeneration
        val itemIdAtReport = currentItemId
        val playSessionIdAtReport = currentPlaySessionId
        val mediaSourceIdAtReport = currentMediaSourceId
        val positionAtReport = player.currentPosition.coerceAtLeast(0L)
        val userIdAtReport = userIdOverride
        // Not `viewModelScope`: `ViewModel.clear()` cancels it *before* calling
        // `onCleared()`, so this launch never ran its body and the server kept the
        // session open — the item stayed as "Now Playing" and the transcode the
        // server had started was never stopped.
        val stopJob = teardownScope.launch {
            val userId = userIdAtReport ?: return@launch
            if ((allowPlaybackChange || isCurrentPlaybackReport(
                    expectedGeneration = generationAtReport,
                    currentGeneration = playbackLoadGeneration,
                    expectedItemId = itemIdAtReport,
                    currentItemId = currentItemId,
                    expectedPlaySessionId = playSessionIdAtReport,
                    currentPlaySessionId = currentPlaySessionId,
                    expectedMediaSourceId = mediaSourceIdAtReport,
                    currentMediaSourceId = currentMediaSourceId,
                )) && itemIdAtReport != null
            ) {
                playbackRepository.reportPlaybackStopped(
                    itemId = itemIdAtReport,
                    playSessionId = playSessionIdAtReport,
                    mediaSourceId = mediaSourceIdAtReport,
                    positionTicks = positionAtReport * 10_000L,
                )
            }
        }
        lastPlaybackStopJob = stopJob
        return stopJob
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
                // O avanço automático já agendado perde para o timer: sem isto a
                // pausa era desfeita um segundo depois pelo episódio seguinte.
                nextEpisodeCountdownJob?.cancel()
                player.pause()
                _state.update { it.afterSleepTimerExpiry() }
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
        // Dismissing the prompt is the point of cancelling: playback has already
        // reached the end, so `isPlaybackEnded` stays true and would keep the
        // overlay on screen.
        _state.update { it.copy(nextEpisodeCountdown = null, nextEpisodePromptDismissed = true) }
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
    private suspend fun invalidatePlaybackForSessionChange(previousUserId: String?) {
        val previousStopJob = if (shouldReportRemotePlaybackBeforeLoad(
                currentItemId = currentItemId,
                isOfflinePlayback = isOfflinePlayback,
                playSessionId = currentPlaySessionId,
                mediaSourceId = currentMediaSourceId,
                stoppedReported = stoppedReported,
            )
        ) {
            reportPlaybackStopped(allowPlaybackChange = true, userIdOverride = previousUserId)
        } else {
            lastPlaybackStopJob?.takeUnless { it.isCompleted }
        }
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
                // A sessão mudou, então o timer não significa mais nada: o job foi
                // cancelado acima e a barra continuaria anunciando "Pausa em 12min"
                // para sempre.
                sleepTimerRemainingMs = null,
                sleepTimerMinutes = null,
                sleepTimerMode = SleepTimerMode.OFF,
            )
        }
        previousStopJob?.join()
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
        if (isOfflinePlayback || networkWasOffline) return
        if (retryJob?.isActive == true) return
        val positionAtError = playbackRetryPosition(
            currentPositionMs = localPlayer.currentPosition,
            errorPositionMs = lastPlaybackPositionAtError,
        )
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
                restoreTrackSelectionOnNextTracksChange = true
                player.prepare()
                player.seekTo(positionAtError)
                player.play()
            }
        }
    }

    private var currentItemChapters: List<Chapter> = emptyList()
    private var currentItemSegments: List<MediaSegment> = emptyList()
}
