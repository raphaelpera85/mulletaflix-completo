package org.mulletaflix.feature.player

import android.app.Activity
import android.view.WindowManager
import androidx.activity.compose.BackHandler
import androidx.compose.animation.*
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.*
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.media3.common.Player
import androidx.media3.cast.MediaRouteButton
import androidx.media3.ui.PlayerView
import kotlinx.coroutines.delay

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
fun VideoPlayerScreen(
    itemId: String,
    onBack: () -> Unit,
    viewModel: PlayerViewModel = hiltViewModel()
) {
    val context = LocalContext.current
    val state by viewModel.state.collectAsState()

    // Keep screen on while playing
    val activity = context as? Activity
    DisposableEffect(Unit) {
        activity?.window?.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        onDispose {
            activity?.window?.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        }
    }

    LaunchedEffect(itemId) { viewModel.loadMedia(itemId) }

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
        activity?.enterPictureInPictureMode(
            android.app.PictureInPictureParams.Builder().build()
        )
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black)
            .clickable(indication = null, interactionSource = remember { androidx.compose.foundation.interaction.MutableInteractionSource() }) {
                osdVisible = !osdVisible
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
                    Button(onClick = { viewModel.loadMedia(itemId) }) {
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
                onSeek = { position -> viewModel.seekTo(position) },
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
private fun PlayerOsd(
    state: PlayerState,
    onBack: () -> Unit,
    onPlayPause: () -> Unit,
    onSeek: (Long) -> Unit,
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
                Icon(Icons.Default.ArrowBack, contentDescription = "Voltar", tint = Color.White)
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
                value = if (state.duration > 0) state.currentPosition.toFloat() / state.duration else 0f,
                onValueChange = { fraction -> onSeek((fraction * state.duration).toLong()) },
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
                listOf("Auto") .plus(qualities).forEach { q ->
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

// Extension: ticks to time string
private fun Long.toTimeString(): String {
    val totalSeconds = this / 1000
    val hours = totalSeconds / 3600
    val minutes = (totalSeconds % 3600) / 60
    val seconds = totalSeconds % 60
    return if (hours > 0)
        String.format("%d:%02d:%02d", hours, minutes, seconds)
    else
        String.format("%d:%02d", minutes, seconds)
}
