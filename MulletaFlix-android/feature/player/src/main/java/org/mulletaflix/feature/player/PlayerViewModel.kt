package org.mulletaflix.feature.player

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.media3.common.MediaItem as Media3Item
import androidx.media3.common.Player
import androidx.media3.common.C
import androidx.media3.common.TrackSelectionOverride
import androidx.media3.cast.CastPlayer
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.trackselection.DefaultTrackSelector
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import android.content.Context
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.core.api.SessionRepository
import javax.inject.Inject

data class TrackInfo(val index: Int, val displayName: String)

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
    val showSkipIntro: Boolean = false,
    val showSkipCredits: Boolean = false,
    val error: String? = null,
)

@HiltViewModel
class PlayerViewModel @Inject constructor(
    @ApplicationContext context: Context,
    private val mediaRepository: MediaRepository,
    private val playbackRepository: PlaybackRepository,
    private val sessionRepository: SessionRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(PlayerState())
    val state: StateFlow<PlayerState> = _state.asStateFlow()

    private val trackSelector = DefaultTrackSelector(context)
    private val localPlayer: ExoPlayer = ExoPlayer.Builder(context)
        .setTrackSelector(trackSelector)
        .build().also { exo ->
        exo.addListener(object : Player.Listener {
            override fun onIsPlayingChanged(isPlaying: Boolean) {
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
                if (playbackState == Player.STATE_ENDED) reportPlaybackStopped()
            }

            override fun onPlayerError(error: androidx.media3.common.PlaybackException) {
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

    private var progressJob: Job? = null
    private var currentItemId: String? = null
    private var currentPlaySessionId: String? = null
    private var currentMediaSourceId: String? = null
    private var stoppedReported = false

    fun loadMedia(itemId: String) {
        currentItemId = itemId
        stoppedReported = false
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first() ?: return@launch

            // Get playback info from server to determine best play method
            val item = mediaRepository.getItem(userId, itemId).getOrNull() ?: return@launch
            _state.update { it.copy(title = item.name, error = null) }

            val playbackInfo = playbackRepository.getPlaybackInfo(itemId, userId).getOrNull()
                ?: return@launch
            val mediaSource = playbackInfo.mediaSources.firstOrNull() ?: return@launch
            val streamUrl = mediaSource.directStreamUrl
                ?: mediaSource.transcodeUrl
                ?: return@launch
            currentPlaySessionId = playbackInfo.playSessionId
            currentMediaSourceId = mediaSource.id

            // Set subtitle tracks from media streams
            val subtitleTracks = item.mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Subtitle }
                .mapIndexed { i, stream -> TrackInfo(i, stream.displayTitle ?: stream.displayLanguage ?: stream.language ?: "Legenda ${i + 1}") }

            val audioTracks = item.mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Audio }
                .mapIndexed { i, stream -> TrackInfo(i, stream.displayTitle ?: stream.displayLanguage ?: stream.language ?: "Áudio ${i + 1}") }

            _state.update {
                it.copy(
                    subtitleTracks = subtitleTracks,
                    audioTracks = audioTracks,
                    availableQualities = qualityOptions(item.mediaStreams + mediaSource.mediaStreams),
                )
            }

            // Prepare Media3. CastPlayer automatically transfers this item when a
            // compatible Cast route is selected by the user.
            val mediaItem = Media3Item.Builder()
                .setUri(streamUrl)
                .build()

            player.setMediaItem(mediaItem)
            player.prepare()

            // Resume from last position
            item.userProgress?.playbackPositionTicks?.let { ticks ->
                val positionMs = ticks / 10_000L
                if (positionMs > 0) player.seekTo(positionMs)
            }

            player.play()

            // Report start to server
            playbackRepository.reportPlaybackStart(
                itemId = itemId,
                playSessionId = currentPlaySessionId,
                mediaSourceId = currentMediaSourceId,
                audioIndex = mediaSource.defaultAudioStreamIndex,
                subtitleIndex = mediaSource.defaultSubtitleStreamIndex,
                positionTicks = player.currentPosition * 10_000L,
            )
        }
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
                    audioIndex = null,
                    subtitleIndex = null,
                    positionTicks = positionMs * 10_000L,
                    isPaused = !player.isPlaying,
                )
            }
        }
    }

    fun skipPrevious() { player.seekToPreviousMediaItem() }

    fun skipNext() { player.seekToNextMediaItem() }

    fun skipSegment() {
        // Jump past the current intro/credits marker
        _state.value.let { s ->
            if (s.showSkipIntro || s.showSkipCredits) {
                // Seek to segment end — markers come from trickplay/MediaSegments
                player.seekTo(player.currentPosition + 30_000)
            }
        }
        _state.update { it.copy(showSkipIntro = false, showSkipCredits = false) }
    }

    fun selectSubtitle(index: Int) {
        _state.update { it.copy(selectedSubtitleIndex = index) }
        selectTrack(index, C.TRACK_TYPE_TEXT)
    }

    fun selectAudio(index: Int) {
        _state.update { it.copy(selectedAudioIndex = index) }
        selectTrack(index, C.TRACK_TYPE_AUDIO)
    }

    fun selectQuality(quality: String) {
        _state.update { it.copy(selectedQuality = quality) }
        val maxBitrate = when (quality.uppercase()) {
            "4K" -> Int.MAX_VALUE
            "1080P", "FULL HD" -> 10_000_000
            "720P", "HD" -> 6_000_000
            "480P", "SD" -> 2_500_000
            else -> Int.MAX_VALUE
        }
        localPlayer.trackSelectionParameters = localPlayer.trackSelectionParameters
            .buildUpon()
            .setMaxVideoBitrate(maxBitrate)
            .build()
    }

    fun setPlaybackSpeed(speed: Float) {
        player.setPlaybackSpeed(speed)
        _state.update { it.copy(playbackSpeed = speed) }
    }

    fun startCast() {
        // The visible MediaRouteButton opens the official system chooser. This
        // method remains as a semantic hook for custom controls and keeps the
        // current position ready for a transfer.
        if (player.currentMediaItem == null) return
        player.seekTo(player.currentPosition)
    }

    private fun selectTrack(index: Int, trackType: Int) {
        val candidates = player.currentTracks.groups
            .filter { it.type == trackType }
            .flatMap { group ->
                (0 until group.length).mapNotNull { trackIndex ->
                    if (group.isTrackSupported(trackIndex)) group to trackIndex else null
                }
            }
        val selected = candidates.getOrNull(index) ?: return
        localPlayer.trackSelectionParameters = localPlayer.trackSelectionParameters
            .buildUpon()
            .setOverrideForType(TrackSelectionOverride(selected.first.mediaTrackGroup, selected.second))
            .build()
    }

    private fun startProgressReporting() {
        progressJob?.cancel()
        progressJob = viewModelScope.launch {
            while (isActive) {
                delay(5_000)
                val userId = sessionRepository.getCurrentUserId().first() ?: continue
                currentItemId?.let { id ->
                    val position = player.currentPosition
                    _state.update { it.copy(currentPosition = position, duration = player.duration.coerceAtLeast(0L)) }
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
                    audioIndex = null,
                    subtitleIndex = null,
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

    override fun onCleared() {
        progressJob?.cancel()
        reportPlaybackStopped()
        player.release()
        super.onCleared()
    }
}
