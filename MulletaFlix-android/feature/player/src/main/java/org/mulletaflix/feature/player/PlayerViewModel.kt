package org.mulletaflix.feature.player

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.media3.common.MediaItem as Media3Item
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import android.content.Context
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.repository.SessionRepository
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

    val player: ExoPlayer = ExoPlayer.Builder(context).build().also { exo ->
        exo.addListener(object : Player.Listener {
            override fun onIsPlayingChanged(isPlaying: Boolean) {
                _state.update { it.copy(isPlaying = isPlaying) }
                if (isPlaying) startProgressReporting() else stopProgressReporting()
            }

            override fun onPlaybackStateChanged(playbackState: Int) {
                _state.update {
                    it.copy(isBuffering = playbackState == Player.STATE_BUFFERING)
                }
            }
        })
    }

    private var progressJob: Job? = null
    private var currentItemId: String? = null

    fun loadMedia(itemId: String) {
        currentItemId = itemId
        viewModelScope.launch {
            val userId = sessionRepository.getUserId() ?: return@launch

            // Get playback info from server to determine best play method
            val item = mediaRepository.getItem(userId, itemId).getOrNull() ?: return@launch
            _state.update { it.copy(title = item.name) }

            val playbackInfo = playbackRepository.getPlaybackInfo(userId, itemId).getOrNull()
            val streamUrl = playbackInfo?.streamUrl ?: return@launch

            // Set subtitle tracks from media streams
            val subtitleTracks = item.mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Subtitle }
                .mapIndexed { i, stream -> TrackInfo(i, stream.displayTitle ?: stream.displayLanguage ?: "Track $i") }

            val audioTracks = item.mediaStreams
                .filter { it.type == org.mulletaflix.domain.model.MediaStreamType.Audio }
                .mapIndexed { i, stream -> TrackInfo(i, stream.displayTitle ?: stream.displayLanguage ?: "Track $i") }

            _state.update { it.copy(subtitleTracks = subtitleTracks, audioTracks = audioTracks) }

            // Prepare ExoPlayer
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
            playbackRepository.reportPlaybackStart(userId, itemId, player.currentPosition)
        }
    }

    fun togglePlayPause() {
        if (player.isPlaying) player.pause() else player.play()
    }

    fun seekTo(positionMs: Long) {
        player.seekTo(positionMs)
        viewModelScope.launch {
            val userId = sessionRepository.getUserId() ?: return@launch
            currentItemId?.let { id ->
                playbackRepository.reportProgress(userId, id, positionMs)
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
        // TODO: switch subtitle track via ExoPlayer TrackSelectionOverride
    }

    fun selectAudio(index: Int) {
        _state.update { it.copy(selectedAudioIndex = index) }
        // TODO: switch audio track via ExoPlayer TrackSelectionOverride
    }

    fun selectQuality(quality: String) {
        _state.update { it.copy(selectedQuality = quality) }
        // TODO: trigger transcode with target bitrate or let HLS adapt
    }

    fun setPlaybackSpeed(speed: Float) {
        player.setPlaybackSpeed(speed)
        _state.update { it.copy(playbackSpeed = speed) }
    }

    fun startCast() {
        // TODO: initiate Google Cast session with current media item
    }

    private fun startProgressReporting() {
        progressJob?.cancel()
        progressJob = viewModelScope.launch {
            while (isActive) {
                delay(5_000)
                val userId = sessionRepository.getUserId() ?: continue
                currentItemId?.let { id ->
                    val position = player.currentPosition
                    _state.update { it.copy(currentPosition = position, duration = player.duration.coerceAtLeast(0L)) }
                    playbackRepository.reportProgress(userId, id, position)
                }
            }
        }
    }

    private fun stopProgressReporting() {
        progressJob?.cancel()
        viewModelScope.launch {
            val userId = sessionRepository.getUserId() ?: return@launch
            currentItemId?.let { id ->
                playbackRepository.reportPlaybackStopped(userId, id, player.currentPosition)
            }
        }
    }

    override fun onCleared() {
        super.onCleared()
        progressJob?.cancel()
        player.release()
    }
}
