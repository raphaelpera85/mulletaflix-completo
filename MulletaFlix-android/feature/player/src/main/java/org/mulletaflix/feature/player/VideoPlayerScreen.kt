package org.mulletaflix.feature.player

import android.app.Activity
import android.media.AudioManager
import android.os.Build
import android.view.WindowManager
import androidx.activity.compose.BackHandler
import androidx.compose.animation.*
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.*
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.cast.MediaRouteButton
import androidx.media3.ui.PlayerView
import kotlinx.coroutines.delay
import kotlin.math.abs
import kotlin.math.roundToInt

/**
 * Full-screen video player screen using Media3 / ExoPlayer.
 *
 * Features:
 *  - HLS adaptive streaming or direct play
 *  - OSD with animated show/hide (3s auto-hide)
 *  - Subtitle track selection
 *  - Audio track selection
 *  - Quality / bitrate selection
 *  - Playback speed control (0.5x – 2x)
 *  - Skip intro / credits (trickplay segment markers)
 *  - Gesture controls: brightness (left), volume (right), seek (horizontal)
 *  - Double-tap: left = -10s, right = +10s
 *  - Picture-in-Picture on home button
 *  - Progress reporting every 5 seconds to the server
 *  - Lock screen OSD (MediaSession via playback service)
 */
@Composable
@UnstableApi
fun VideoPlayerScreen(
    itemId: String,
    onBack: () -> Unit,
    offlineUri: String? = null,
    offlineTitle: String? = null,
    viewModel: PlayerViewModel = hiltViewModel()
) {
    val context = LocalContext.current
    val state by viewModel.state.collectAsStateWithLifecycle()
    val latestPosition by rememberUpdatedState(state.currentPosition)
    val latestDuration by rememberUpdatedState(state.duration)
    val latestPlaying by rememberUpdatedState(state.isPlaying)
    val latestPipEnabled by rememberUpdatedState(state.pictureInPictureEnabled)
    var gestureHint by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(gestureHint) {
        if (gestureHint != null) {
            delay(900)
            gestureHint = null
        }
    }

    // Keep screen on while playing
    val activity = context as? Activity
    DisposableEffect(Unit) {
        activity?.window?.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        PlayerPictureInPictureController.register {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O &&
                shouldEnterPictureInPicture(latestPipEnabled, latestPlaying, Build.VERSION.SDK_INT)
            ) {
                activity?.enterPictureInPictureMode(
                    android.app.PictureInPictureParams.Builder().build()
                )
            }
        }
        onDispose {
            activity?.window?.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
            PlayerPictureInPictureController.unregister()
        }
    }

    LaunchedEffect(itemId, offlineUri) {
        if (offlineUri != null) viewModel.loadOffline(offlineUri, offlineTitle ?: itemId)
        else viewModel.loadMedia(itemId)
    }

    // OSD visibility auto-hide
    var osdVisible by remember { mutableStateOf(true) }
    LaunchedEffect(osdVisible, state.isPlaying) {
        if (osdVisible && state.isPlaying) {
            delay(3000)
            osdVisible = false
        }
    }

    // PiP on back when playing
    BackHandler(enabled = state.isPlaying) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O &&
            shouldEnterPictureInPicture(state.pictureInPictureEnabled, state.isPlaying, Build.VERSION.SDK_INT)
        ) {
            activity?.enterPictureInPictureMode(
                android.app.PictureInPictureParams.Builder().build()
            )
        } else {
            onBack()
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black)
            .pointerInput(Unit) {
                var startX = 0f
                var startPosition = 0L
                var previewPosition = 0L
                var totalDrag = Offset.Zero
                var horizontalDrag = false
                var axisLocked = false
                detectDragGestures(
                    onDragStart = {
                        startX = it.x
                        startPosition = latestPosition
                        previewPosition = latestPosition
                        totalDrag = Offset.Zero
                        horizontalDrag = false
                        axisLocked = false
                    },
                    onDragCancel = {},
                    onDragEnd = {
                        if (horizontalDrag) viewModel.seekTo(previewPosition)
                    },
                    onDrag = { _, dragAmount ->
                        totalDrag += dragAmount
                        if (!axisLocked && totalDrag.getDistance() >= 12f) {
                            axisLocked = true
                            horizontalDrag = abs(totalDrag.x) > abs(totalDrag.y)
                        }

                        if (horizontalDrag) {
                            val delta = seekDeltaFromHorizontalDrag(
                                dragPixels = totalDrag.x,
                                viewportWidthPixels = size.width.toFloat(),
                                durationMs = latestDuration,
                            )
                            previewPosition = (startPosition + delta).coerceIn(
                                0L,
                                latestDuration.coerceAtLeast(0L),
                            )
                            viewModel.previewSeekTo(previewPosition)
                            gestureHint = "${previewPosition.toTimeString()} / ${latestDuration.toTimeString()}"
                            return@detectDragGestures
                        }

                        // Up increases the level; down decreases it. The side
                        // of the screen selects brightness or media volume.
                        val delta = -dragAmount.y / 900f
                        if (startX < size.width / 2f) {
                            val window = activity?.window
                            if (window != null) {
                                val attributes = window.attributes
                                val current = attributes.screenBrightness.takeIf { it >= 0f } ?: 0.5f
                                attributes.screenBrightness = adjustBrightness(current, delta)
                                window.attributes = attributes
                                gestureHint = "Brilho ${(attributes.screenBrightness * 100).roundToInt()}%"
                            }
                        } else {
                            val audioManager = context.getSystemService(AudioManager::class.java)
                            if (audioManager != null) {
                                val current = audioManager.getStreamVolume(AudioManager.STREAM_MUSIC)
                                val maximum = audioManager.getStreamMaxVolume(AudioManager.STREAM_MUSIC)
                                val next = adjustVolume(current, maximum, delta)
                                audioManager.setStreamVolume(AudioManager.STREAM_MUSIC, next, 0)
                                gestureHint = "Volume ${(next * 100f / maximum.coerceAtLeast(1)).roundToInt()}%"
                            }
                        }
                    },
                )
            }
            .pointerInput(Unit) {
                detectTapGestures(
                    onTap = { osdVisible = !osdVisible },
                    onDoubleTap = { offset ->
                        val seekDelta = if (offset.x < size.width / 2f) -10_000L else 10_000L
                        val target = (latestPosition + seekDelta).coerceIn(
                            0L,
                            latestDuration.coerceAtLeast(0L),
                        )
                        viewModel.seekTo(target)
                        gestureHint = if (seekDelta < 0) "−10 segundos" else "+10 segundos"
                    },
                )
            }
    ) {

        // ── ExoPlayer Surface ───────────────────────────────────────────────
        AndroidView(
            factory = { ctx ->
                PlayerView(ctx).apply {
                    useController = false  // We use our own OSD
                    player = viewModel.player
                }
            },
            update = { playerView ->
                playerView.subtitleView?.setFractionalTextSize(
                    0.0533f * state.subtitleFontSize.coerceIn(50, 200) / 100f,
                )
            },
            modifier = Modifier.fillMaxSize()
        )

        // ── Loading indicator ────────────────────────────────────────────────
        AnimatedVisibility(visible = state.isBuffering) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator(color = MaterialTheme.colorScheme.secondary)
            }
        }

        AnimatedVisibility(visible = state.error != null, modifier = Modifier.align(Alignment.Center)) {
            Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)) {
                Column(
                    modifier = Modifier.padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Text(state.error ?: "Erro de reprodução", color = MaterialTheme.colorScheme.onSurface)
                    Button(onClick = {
                        if (offlineUri != null) viewModel.loadOffline(offlineUri, offlineTitle ?: itemId)
                        else viewModel.loadMedia(itemId)
                    }) {
                        Text("Tentar novamente")
                    }
                }
            }
        }

        // ── Skip Intro button ────────────────────────────────────────────────
        AnimatedVisibility(
            visible = state.showSkipIntro,
            enter = slideInHorizontally { it },
            exit = slideOutHorizontally { it },
            modifier = Modifier.align(Alignment.BottomEnd).padding(end = 48.dp, bottom = 120.dp)
        ) {
            Button(
                onClick = { viewModel.skipSegment() },
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
            ) {
                Text("Pular Introdução")
                Icon(Icons.Default.SkipNext, contentDescription = null, modifier = Modifier.padding(start = 4.dp))
            }
        }

        gestureHint?.let { hint ->
            Surface(
                modifier = Modifier.align(Alignment.Center),
                color = Color.Black.copy(alpha = 0.72f),
                shape = androidx.compose.foundation.shape.RoundedCornerShape(12.dp),
            ) {
                Text(hint, color = Color.White, modifier = Modifier.padding(horizontal = 18.dp, vertical = 10.dp))
            }
        }

        // ── Skip Credits button ──────────────────────────────────────────────
        AnimatedVisibility(
            visible = state.showSkipCredits,
            enter = slideInHorizontally { it },
            exit = slideOutHorizontally { it },
            modifier = Modifier.align(Alignment.BottomEnd).padding(end = 48.dp, bottom = 120.dp)
        ) {
            Button(
                onClick = { viewModel.skipSegment() },
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.primary)
            ) {
                Text("Pular Créditos")
                Icon(Icons.Default.SkipNext, contentDescription = null, modifier = Modifier.padding(start = 4.dp))
            }
        }

        // ── Next Episode Auto-Play Prompt ────────────────────────────────────
        val nextEpisode = state.nextEpisode
        val showNextEpisode = nextEpisode != null && shouldShowNextEpisodePrompt(
            hasNextEpisode = true,
            isPlaybackEnded = !state.isPlaying && state.currentPosition > 0 && state.duration > 0 && state.currentPosition >= state.duration - 1500L,
            countdownActive = state.nextEpisodeCountdown != null,
        )
        AnimatedVisibility(
            visible = showNextEpisode,
            enter = slideInVertically { it } + fadeIn(),
            exit = slideOutVertically { it } + fadeOut(),
            modifier = Modifier.align(Alignment.BottomEnd).padding(end = 32.dp, bottom = 100.dp)
        ) {
            if (nextEpisode != null) {
                Card(
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f)),
                    shape = androidx.compose.foundation.shape.RoundedCornerShape(16.dp),
                    modifier = Modifier.widthIn(max = 340.dp)
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.PlayCircle, contentDescription = null, tint = MaterialTheme.colorScheme.primary)
                            Spacer(Modifier.width(8.dp))
                            Text(
                                "Próximo Episódio",
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.primary,
                            )
                        }
                        Text(
                            text = nextEpisode.title,
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 4.dp),
                        )
                        formatNextEpisodeSubtitle(nextEpisode.seasonNumber, nextEpisode.episodeNumber)?.let { subtitle ->
                            Text(
                                text = subtitle,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                        state.nextEpisodeCountdown?.let { seconds ->
                            Text(
                                text = "Reproduzindo em ${seconds}s...",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.secondary,
                                modifier = Modifier.padding(top = 6.dp),
                            )
                        }
                        Row(
                            horizontalArrangement = Arrangement.End,
                            modifier = Modifier.fillMaxWidth().padding(top = 12.dp)
                        ) {
                            TextButton(onClick = { viewModel.cancelNextEpisodeCountdown() }) {
                                Text("Cancelar")
                            }
                            Spacer(Modifier.width(8.dp))
                            Button(onClick = { viewModel.playNextEpisodeNow() }) {
                                Text("Assistir Agora")
                            }
                        }
                    }
                }
            }
        }

        // ── OSD (On-Screen Display) ──────────────────────────────────────────
        AnimatedVisibility(
            visible = osdVisible,
            enter = fadeIn(tween(200)),
            exit = fadeOut(tween(300))
        ) {
            PlayerOsd(
                state = state,
                onBack = onBack,
                onPlayPause = { viewModel.togglePlayPause() },
                onSeekPreview = { position -> viewModel.previewSeekTo(position) },
                onSeekFinished = { position -> viewModel.seekTo(position) },
                onPrevious = { viewModel.skipPrevious() },
                onNext = { viewModel.skipNext() },
                onSubtitleSelect = { index -> viewModel.selectSubtitle(index) },
                onAudioSelect = { index -> viewModel.selectAudio(index) },
                onQualitySelect = { quality -> viewModel.selectQuality(quality) },
                onSpeedSelect = { speed -> viewModel.setPlaybackSpeed(speed) },
                onCastClick = { viewModel.startCast() }
            )
        }
    }
}

@Composable
@UnstableApi
private fun PlayerOsd(
    state: PlayerState,
    onBack: () -> Unit,
    onPlayPause: () -> Unit,
    onSeekPreview: (Long) -> Unit,
    onSeekFinished: (Long) -> Unit,
    onPrevious: () -> Unit,
    onNext: () -> Unit,
    onSubtitleSelect: (Int) -> Unit,
    onAudioSelect: (Int) -> Unit,
    onQualitySelect: (String) -> Unit,
    onSpeedSelect: (Float) -> Unit,
    onCastClick: () -> Unit,
) {
    var showSubtitleMenu by remember { mutableStateOf(false) }
    var showAudioMenu by remember { mutableStateOf(false) }
    var showQualityMenu by remember { mutableStateOf(false) }
    var showSpeedMenu by remember { mutableStateOf(false) }
    var isSeeking by remember { mutableStateOf(false) }
    var seekFraction by remember(state.duration) {
        mutableFloatStateOf(
            if (state.duration > 0) state.currentPosition.toFloat() / state.duration else 0f,
        )
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.4f))
    ) {

        // ── Top bar ──────────────────────────────────────────────────────────
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp)
                .align(Alignment.TopCenter),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar", tint = Color.White)
            }
            Text(
                text = state.title ?: "",
                style = MaterialTheme.typography.titleMedium,
                color = Color.White
            )
            Row {
                // Official Media3 Cast button: opens the system device chooser
                // and lets CastPlayer transfer the current media item.
                MediaRouteButton(
                    modifier = Modifier.size(48.dp),
                )
                // Audio tracks
                IconButton(onClick = { showAudioMenu = true }) {
                    Icon(Icons.Default.Audiotrack, contentDescription = "Áudio", tint = Color.White)
                }
                // Subtitles
                IconButton(onClick = { showSubtitleMenu = true }) {
                    Icon(Icons.Default.ClosedCaption, contentDescription = "Legendas", tint = Color.White)
                }
                // Quality
                IconButton(onClick = { showQualityMenu = true }) {
                    Icon(Icons.Default.Hd, contentDescription = "Qualidade", tint = Color.White)
                }
                // Speed
                IconButton(onClick = { showSpeedMenu = true }) {
                    Icon(Icons.Default.Speed, contentDescription = "Velocidade", tint = Color.White)
                }
            }
        }

        // ── Center controls ──────────────────────────────────────────────────
        Row(
            modifier = Modifier.align(Alignment.Center),
            horizontalArrangement = Arrangement.spacedBy(32.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onPrevious, modifier = Modifier.size(48.dp)) {
                Icon(Icons.Default.SkipPrevious, contentDescription = "Anterior", tint = Color.White, modifier = Modifier.size(36.dp))
            }
            // Play / Pause
            IconButton(
                onClick = onPlayPause,
                modifier = Modifier
                    .size(72.dp)
                    .background(Color.White.copy(alpha = 0.2f), shape = androidx.compose.foundation.shape.CircleShape)
            ) {
                Icon(
                    if (state.isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                    contentDescription = if (state.isPlaying) "Pausar" else "Reproduzir",
                    tint = Color.White,
                    modifier = Modifier.size(48.dp)
                )
            }
            IconButton(onClick = onNext, modifier = Modifier.size(48.dp)) {
                Icon(Icons.Default.SkipNext, contentDescription = "Próximo", tint = Color.White, modifier = Modifier.size(36.dp))
            }
        }

        // ── Bottom seek bar + time ────────────────────────────────────────────
        Column(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 24.dp)
        ) {
            // Time labels
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(state.currentPosition.toTimeString(), color = Color.White, style = MaterialTheme.typography.labelMedium)
                Text(state.duration.toTimeString(), color = Color.White.copy(0.7f), style = MaterialTheme.typography.labelMedium)
            }
            // Seek bar
            Slider(
                value = if (isSeeking) seekFraction else {
                    if (state.duration > 0) state.currentPosition.toFloat() / state.duration else 0f
                },
                onValueChange = { fraction ->
                    isSeeking = true
                    seekFraction = fraction
                    onSeekPreview(seekPositionFromFraction(fraction, state.duration))
                },
                onValueChangeFinished = {
                    isSeeking = false
                    onSeekFinished(seekPositionFromFraction(seekFraction, state.duration))
                },
                colors = SliderDefaults.colors(
                    thumbColor = MaterialTheme.colorScheme.secondary,
                    activeTrackColor = MaterialTheme.colorScheme.secondary,
                    inactiveTrackColor = Color.White.copy(0.3f)
                ),
                modifier = Modifier.fillMaxWidth()
            )
        }

        // ── Subtitle track dropdown ───────────────────────────────────────────
        if (showSubtitleMenu) {
            PlayerTrackMenu(
                title = "Legendas",
                tracks = state.subtitleTracks,
                selectedIndex = state.selectedSubtitleIndex,
                onSelect = { onSubtitleSelect(it); showSubtitleMenu = false },
                onDismiss = { showSubtitleMenu = false }
            )
        }

        // ── Audio track dropdown ──────────────────────────────────────────────
        if (showAudioMenu) {
            PlayerTrackMenu(
                title = "Faixa de Áudio",
                tracks = state.audioTracks,
                selectedIndex = state.selectedAudioIndex,
                allowNone = false,
                onSelect = { onAudioSelect(it); showAudioMenu = false },
                onDismiss = { showAudioMenu = false }
            )
        }

        // ── Quality dropdown ─────────────────────────────────────────────────
        if (showQualityMenu) {
            QualityMenu(
                qualities = state.availableQualities,
                selectedQuality = state.selectedQuality,
                onSelect = { onQualitySelect(it); showQualityMenu = false },
                onDismiss = { showQualityMenu = false }
            )
        }

        // ── Speed dropdown ───────────────────────────────────────────────────
        if (showSpeedMenu) {
            SpeedMenu(
                currentSpeed = state.playbackSpeed,
                onSelect = { onSpeedSelect(it); showSpeedMenu = false },
                onDismiss = { showSpeedMenu = false }
            )
        }
    }
}

// Helpers
@Composable
private fun PlayerTrackMenu(
    title: String,
    tracks: List<TrackInfo>,
    selectedIndex: Int,
    allowNone: Boolean = true,
    onSelect: (Int) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = {
            Column {
                if (allowNone) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.clickable { onSelect(-1) }.fillMaxWidth().padding(vertical = 8.dp)
                    ) {
                        RadioButton(selected = selectedIndex == -1, onClick = { onSelect(-1) })
                        Text("Nenhuma", modifier = Modifier.padding(start = 8.dp))
                    }
                }
                tracks.forEachIndexed { index, track ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.clickable { onSelect(index) }.fillMaxWidth().padding(vertical = 8.dp)
                    ) {
                        RadioButton(selected = selectedIndex == index, onClick = { onSelect(index) })
                        Text(track.displayName, modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = {}
    )
}

@Composable
private fun QualityMenu(
    qualities: List<String>,
    selectedQuality: String?,
    onSelect: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Qualidade") },
        text = {
            Column {
                qualityMenuOptions(qualities).forEach { q ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.clickable { onSelect(q) }.fillMaxWidth().padding(vertical = 8.dp)
                    ) {
                        RadioButton(selected = selectedQuality == q, onClick = { onSelect(q) })
                        Text(q, modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = {}
    )
}

@Composable
private fun SpeedMenu(
    currentSpeed: Float,
    onSelect: (Float) -> Unit,
    onDismiss: () -> Unit,
) {
    val speeds = listOf(0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f)
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Velocidade de Reprodução") },
        text = {
            Column {
                speeds.forEach { speed ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.clickable { onSelect(speed) }.fillMaxWidth().padding(vertical = 8.dp)
                    ) {
                        RadioButton(selected = currentSpeed == speed, onClick = { onSelect(speed) })
                        Text("${speed}x", modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = {}
    )
}

// Extension: millis to time string
private fun Long.toTimeString(): String =
    org.mulletaflix.core.common.util.FormatUtils.formatDuration(this)

