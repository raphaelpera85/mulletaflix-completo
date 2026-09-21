package org.mulletaflix.feature.player

import android.Manifest
import android.app.Activity
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.ActivityInfo
import android.content.pm.PackageManager
import android.content.res.Configuration
import android.media.AudioManager
import android.os.Build
import android.view.WindowManager
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.*
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.cast.MediaRouteButton
import androidx.media3.ui.PlayerView
import androidx.media3.ui.CaptionStyleCompat
import kotlinx.coroutines.delay
import kotlin.math.abs
import kotlin.math.roundToInt
import org.mulletaflix.designsystem.theme.MulletaFlixRed

internal const val CAST_ACTION_CONTENT_DESCRIPTION = "Transmitir para dispositivo compatível"
internal const val PLAYER_TOP_BAR_ACTIONS_CONTENT_DESCRIPTION = "Ações do player; deslize horizontalmente para ver mais"
internal const val PLAYBACK_STATS_CONTENT_DESCRIPTION = "Dados técnicos da mídia; deslize verticalmente para ver mais"
internal const val PLAYER_OSD_AUTO_HIDE_MILLIS = 3000L
private const val NOTIFICATION_PROMPT_PREFERENCES = "player_notification_preferences"
private const val NOTIFICATION_PROMPT_DISMISSED_KEY = "permission_prompt_dismissed"

internal fun shouldAutoHidePlayerOsd(isTelevision: Boolean, isPlaying: Boolean): Boolean =
    !isTelevision && isPlaying

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
    val configuration = LocalConfiguration.current
    val isTelevision = (configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
        Configuration.UI_MODE_TYPE_TELEVISION
    val state by viewModel.state.collectAsStateWithLifecycle()
    val notificationPreferences = remember(context) {
        context.getSharedPreferences(NOTIFICATION_PROMPT_PREFERENCES, Context.MODE_PRIVATE)
    }
    val latestPosition by rememberUpdatedState(state.currentPosition)
    val latestDuration by rememberUpdatedState(state.duration)
    val latestPlaying by rememberUpdatedState(state.isPlaying)
    val latestPipEnabled by rememberUpdatedState(state.pictureInPictureEnabled)
    var gestureHint by remember { mutableStateOf<String?>(null) }
    var notificationPromptDismissed by remember(notificationPreferences) {
        mutableStateOf(
            notificationPreferences.getBoolean(NOTIFICATION_PROMPT_DISMISSED_KEY, false),
        )
    }
    var notificationPermissionGranted by remember {
        mutableStateOf(
            Build.VERSION.SDK_INT < 33 ||
                context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED,
        )
    }
    val notificationPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
    ) { granted ->
        notificationPermissionGranted = granted
        notificationPromptDismissed = !granted
        notificationPreferences.edit()
            .putBoolean(NOTIFICATION_PROMPT_DISMISSED_KEY, !granted)
            .apply()
    }
    val showNotificationPrompt =
        shouldShowNotificationPermissionPrompt(
            sdkInt = Build.VERSION.SDK_INT,
            permissionGranted = notificationPermissionGranted,
            promptDismissed = notificationPromptDismissed,
        )
    LaunchedEffect(gestureHint) {
        if (gestureHint != null) {
            delay(900)
            gestureHint = null
        }
    }

    // Keep screen on while playing
    val activity = context as? Activity
    val previousOrientation = remember(activity) {
        activity?.requestedOrientation ?: ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
    }
    DisposableEffect(activity) {
        activity?.window?.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        activity?.requestedOrientation = playerOrientationForEntry(previousOrientation)
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
            activity?.requestedOrientation = previousOrientation
            PlayerPictureInPictureController.unregister()
        }
    }

    LaunchedEffect(itemId, offlineUri) {
        if (offlineUri != null) viewModel.loadOffline(offlineUri, offlineTitle ?: itemId)
        else viewModel.loadMedia(itemId)
    }

    // OSD visibility auto-hide
    var osdVisible by remember { mutableStateOf(true) }
    LaunchedEffect(osdVisible, state.isPlaying, isTelevision) {
        if (osdVisible && shouldAutoHidePlayerOsd(isTelevision, state.isPlaying)) {
            delay(PLAYER_OSD_AUTO_HIDE_MILLIS)
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
                        if (state.isControlsLocked) return@detectDragGestures
                        startX = it.x
                        startPosition = latestPosition
                        previewPosition = latestPosition
                        totalDrag = Offset.Zero
                        horizontalDrag = false
                        axisLocked = false
                    },
                    onDragCancel = {},
                    onDragEnd = {
                        if (state.isControlsLocked) return@detectDragGestures
                        if (horizontalDrag) viewModel.seekTo(previewPosition)
                    },
                    onDrag = { _, dragAmount ->
                        if (state.isControlsLocked) return@detectDragGestures
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
                    onTap = {
                        if (!state.isControlsLocked) {
                            osdVisible = !osdVisible
                        }
                    },
                    onDoubleTap = { offset ->
                        if (state.isControlsLocked) return@detectTapGestures
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
                playerView.resizeMode = state.aspectRatio.resizeMode
                playerView.subtitleView?.setFractionalTextSize(
                    0.0533f * state.subtitleFontSize.coerceIn(50, 200) / 100f,
                )
                playerView.subtitleView?.setStyle(
                    CaptionStyleCompat(
                        subtitleForegroundColor(state.subtitleColor),
                        android.graphics.Color.TRANSPARENT,
                        android.graphics.Color.TRANSPARENT,
                        CaptionStyleCompat.EDGE_TYPE_OUTLINE,
                        android.graphics.Color.BLACK,
                        null,
                    )
                )
            },
            modifier = Modifier.fillMaxSize()
        )

        AnimatedVisibility(
            visible = showNotificationPrompt && osdVisible,
            enter = fadeIn(),
            exit = fadeOut(),
            modifier = Modifier.align(Alignment.TopCenter).padding(top = 24.dp),
        ) {
            Surface(
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.96f),
                shape = androidx.compose.foundation.shape.RoundedCornerShape(16.dp),
                tonalElevation = 6.dp,
            ) {
                Row(
                    modifier = Modifier.padding(start = 14.dp, end = 6.dp, top = 8.dp, bottom = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    Text(
                        text = "Ative as notificações para controles de mídia e downloads.",
                        style = MaterialTheme.typography.bodySmall,
                        modifier = Modifier.widthIn(max = 220.dp),
                    )
                    TextButton(
                        onClick = { notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS) },
                    ) {
                        Text("Ativar")
                    }
                    IconButton(
                        onClick = {
                            notificationPromptDismissed = true
                            notificationPreferences.edit()
                                .putBoolean(NOTIFICATION_PROMPT_DISMISSED_KEY, true)
                                .apply()
                        },
                    ) {
                        Icon(Icons.Default.Close, contentDescription = "Agora não")
                    }
                }
            }
        }

        // ── Floating Unlock Button when Screen is Locked ─────────────────────
        AnimatedVisibility(
            visible = state.isControlsLocked,
            enter = fadeIn(),
            exit = fadeOut(),
            modifier = Modifier.align(Alignment.TopStart).padding(24.dp)
        ) {
            IconButton(
                onClick = { viewModel.setControlsLocked(false) },
                modifier = Modifier
                    .size(56.dp)
                    .background(Color.Black.copy(alpha = 0.65f), shape = androidx.compose.foundation.shape.CircleShape)
            ) {
                Icon(
                    Icons.Default.Lock,
                    contentDescription = "Desbloquear controles",
                    tint = MaterialTheme.colorScheme.secondary,
                    modifier = Modifier.size(32.dp)
                )
            }
        }

        // ── Loading indicator ────────────────────────────────────────────────
        AnimatedVisibility(visible = state.isBuffering) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator(color = MaterialTheme.colorScheme.secondary)
            }
        }

        AnimatedVisibility(
            visible = state.isNetworkOffline,
            modifier = Modifier.align(Alignment.TopCenter).padding(top = 24.dp),
        ) {
            Surface(
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
                shape = androidx.compose.foundation.shape.RoundedCornerShape(24.dp),
                tonalElevation = 4.dp,
                modifier = Modifier.semantics { contentDescription = "Sem conexão. Tentando reconectar." },
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    Icon(Icons.Default.WifiOff, contentDescription = null, tint = MaterialTheme.colorScheme.secondary)
                    Text("Sem conexão — tentando reconectar…", color = MaterialTheme.colorScheme.onSurface)
                }
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
                        else viewModel.retryPlayback()
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
                colors = ButtonDefaults.buttonColors(containerColor = MulletaFlixRed)
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
                colors = ButtonDefaults.buttonColors(containerColor = MulletaFlixRed)
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
                            Icon(Icons.Default.PlayCircle, contentDescription = null, tint = MaterialTheme.colorScheme.secondary)
                            Spacer(Modifier.width(8.dp))
                            Text(
                                "Próximo Episódio",
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.secondary,
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
            visible = osdVisible && !state.isControlsLocked,
            enter = fadeIn(tween(200)),
            exit = fadeOut(tween(300))
        ) {
            PlayerOsd(
                state = state,
                onBack = onBack,
                onPlayPause = { viewModel.togglePlayPause() },
                onSeekPreview = { position -> viewModel.previewSeekTo(position) },
                onSeekFinished = { position -> viewModel.seekTo(position) },
                onSeekBy = { delta -> viewModel.seekBy(delta) },
                onPrevious = { viewModel.skipPrevious() },
                onNext = { viewModel.skipNext() },
                onPreviousChapter = { viewModel.skipToPreviousChapter() },
                onNextChapter = { viewModel.skipToNextChapter() },
                onSubtitleSelect = { index -> viewModel.selectSubtitle(index) },
                onAudioSelect = { index -> viewModel.selectAudio(index) },
                onQualitySelect = { quality -> viewModel.selectQuality(quality) },
                onSpeedSelect = { speed -> viewModel.setPlaybackSpeed(speed) },
                onSleepTimerSelect = { minutes -> viewModel.setSleepTimer(minutes) },
                onSleepTimerAtMediaEnd = { viewModel.setSleepTimerAtMediaEnd() },
                onAspectRatioSelect = { ratio -> viewModel.setAspectRatio(ratio) },
                onLockClick = { viewModel.setControlsLocked(true) },
                onCastClick = { viewModel.startCast() },
                onCopyStats = { copyPlaybackStats(context, state.title, state.playbackStats) },
                onShareStats = { sharePlaybackStats(context, state.title, state.playbackStats) },
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
    onSeekBy: (Long) -> Unit,
    onPrevious: () -> Unit,
    onNext: () -> Unit,
    onPreviousChapter: () -> Unit,
    onNextChapter: () -> Unit,
    onSubtitleSelect: (Int) -> Unit,
    onAudioSelect: (Int) -> Unit,
    onQualitySelect: (String) -> Unit,
    onSpeedSelect: (Float) -> Unit,
    onSleepTimerSelect: (Int?) -> Unit,
    onSleepTimerAtMediaEnd: () -> Unit,
    onAspectRatioSelect: (VideoAspectRatio) -> Unit,
    onLockClick: () -> Unit,
    onCastClick: () -> Unit,
    onCopyStats: () -> Unit,
    onShareStats: () -> Unit,
) {
    var showSubtitleMenu by remember { mutableStateOf(false) }
    var showAudioMenu by remember { mutableStateOf(false) }
    var showQualityMenu by remember { mutableStateOf(false) }
    var showSpeedMenu by remember { mutableStateOf(false) }
    var showSleepTimerMenu by remember { mutableStateOf(false) }
    var showAspectRatioMenu by remember { mutableStateOf(false) }
    var showStatsDialog by remember { mutableStateOf(false) }
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
                .padding(horizontal = 16.dp, vertical = 8.dp)
                .align(Alignment.TopCenter),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar", tint = Color.White)
            }
            Column(
                modifier = Modifier.weight(1f).padding(horizontal = 8.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text(
                    text = state.title ?: "",
                    style = MaterialTheme.typography.titleMedium,
                    color = Color.White,
                    maxLines = 1,
                )
                state.currentChapterName?.let { chapterName ->
                    Text(
                        text = chapterName,
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.secondary,
                        maxLines = 1,
                    )
                }
            }
            PlayerTopBarActionsRow(
                modifier = Modifier.weight(1f),
            ) {
                // Official Media3 Cast button. Keep a text label beside it so
                // the action remains discoverable on mobile and TV layouts.
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier
                        .padding(end = 4.dp)
                        .semantics(mergeDescendants = true) {
                                contentDescription = castActionContentDescription(state.isCasting)
                        },
                ) {
                    MediaRouteButton(
                        modifier = Modifier.size(40.dp),
                    )
                    Text(
                        text = castActionLabel(state.isCasting),
                        color = Color.White,
                        style = MaterialTheme.typography.labelSmall,
                    )
                }
                // Aspect ratio
                IconButton(onClick = { showAspectRatioMenu = true }) {
                    Icon(Icons.Default.AspectRatio, contentDescription = "Proporção", tint = Color.White)
                }
                // Audio tracks
                IconButton(
                    onClick = { showAudioMenu = true },
                    enabled = state.audioTracks.isNotEmpty(),
                ) {
                    Icon(Icons.Default.Audiotrack, contentDescription = "Áudio", tint = Color.White)
                }
                // Subtitles
                IconButton(
                    onClick = { showSubtitleMenu = true },
                    enabled = state.subtitleTracks.isNotEmpty(),
                ) {
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
                // Sleep timer
                IconButton(onClick = { showSleepTimerMenu = true }) {
                    Icon(
                        Icons.Default.Bedtime,
                        contentDescription = sleepTimerDisplayLabel(state.sleepTimerMode, state.sleepTimerRemainingMs)
                            ?: "Temporizador de suspensão",
                        tint = if (state.sleepTimerMode != SleepTimerMode.OFF) {
                            MaterialTheme.colorScheme.secondary
                        } else {
                            Color.White
                        },
                    )
                }
                // Playback stats
                IconButton(onClick = { showStatsDialog = true }) {
                    Icon(Icons.Default.Info, contentDescription = "Estatísticas", tint = Color.White)
                }
                // Lock screen
                IconButton(onClick = onLockClick) {
                    Icon(Icons.Default.LockOpen, contentDescription = "Bloquear controles", tint = Color.White)
                }
            }
        }

        // ── Center controls ──────────────────────────────────────────────────
        PlayerTransportControls(
            modifier = Modifier.align(Alignment.Center),
            isPlaying = state.isPlaying,
            hasChapters = state.chapters.isNotEmpty(),
            onSeekBy = onSeekBy,
            onPrevious = onPrevious,
            onPlayPause = onPlayPause,
            onNext = onNext,
            onPreviousChapter = onPreviousChapter,
            onNextChapter = onNextChapter,
        )

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
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(state.currentPosition.toTimeString(), color = Color.White, style = MaterialTheme.typography.labelMedium)
                state.estimatedEndTime?.let { endTime ->
                    Text(endTime, color = MaterialTheme.colorScheme.secondary, style = MaterialTheme.typography.labelMedium)
                }
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
                isMetered = state.isNetworkMetered,
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

        if (showSleepTimerMenu) {
            SleepTimerMenu(
                remainingMs = state.sleepTimerRemainingMs,
                selectedMinutes = state.sleepTimerMinutes,
                isAtMediaEnd = state.sleepTimerMode == SleepTimerMode.AT_MEDIA_END,
                onSelect = { minutes ->
                    onSleepTimerSelect(minutes)
                    showSleepTimerMenu = false
                },
                onSelectAtMediaEnd = {
                    onSleepTimerAtMediaEnd()
                    showSleepTimerMenu = false
                },
                onDismiss = { showSleepTimerMenu = false },
            )
        }

        // ── Aspect Ratio dropdown ─────────────────────────────────────────────
        if (showAspectRatioMenu) {
            AspectRatioMenu(
                currentRatio = state.aspectRatio,
                onSelect = { onAspectRatioSelect(it); showAspectRatioMenu = false },
                onDismiss = { showAspectRatioMenu = false }
            )
        }

        // ── Playback Stats Dialog ─────────────────────────────────────────────
        if (showStatsDialog) {
            PlaybackStatsDialog(
                title = state.title,
                stats = state.playbackStats,
                onCopy = onCopyStats,
                onShare = onShareStats,
                onDismiss = { showStatsDialog = false }
            )
        }
    }
}

/**
 * Center transport controls of the player OSD.
 *
 * The controls deliberately carry **no** explicit `.size(...)`, so the tappable
 * area comes from Material 3's `minimumInteractiveComponentSize()` and only the
 * icon glyph is sized.
 *
 * A measured note, because it was investigated and the first assumption was
 * wrong: an explicit `.size(44.dp)` on an `IconButton` does **not** shrink the
 * touch target below 48 dp — Material still supplies the minimum. Verified on
 * the Android TV AVD during the v1.2.45 round by rendering the control with
 * `size(44.dp)` and with `size(30.dp)`: neither dropped the tappable bounds
 * under 48 dp. So there was no touch-target defect here to fix, and no test was
 * kept for it — a test that passes with and without the change proves nothing.
 * What remains valuable is the extraction itself: these controls are now a
 * standalone `internal` composable instead of being buried in the ~350-line
 * `PlayerOsd`, so they can be exercised directly in the future.
 */
@Composable
internal fun PlayerTransportControls(
    modifier: Modifier = Modifier,
    isPlaying: Boolean = false,
    hasChapters: Boolean = false,
    onSeekBy: (Long) -> Unit = {},
    onPrevious: () -> Unit = {},
    onPlayPause: () -> Unit = {},
    onNext: () -> Unit = {},
    onPreviousChapter: () -> Unit = {},
    onNextChapter: () -> Unit = {},
) {
    Row(
        modifier = modifier,
        horizontalArrangement = Arrangement.spacedBy(20.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        IconButton(onClick = { onSeekBy(-10_000L) }) {
            Icon(
                Icons.Default.Replay10,
                contentDescription = "Voltar 10 segundos",
                tint = Color.White,
                modifier = Modifier.size(34.dp),
            )
        }
        if (hasChapters) {
            IconButton(onClick = onPreviousChapter) {
                Icon(Icons.Default.FastRewind, contentDescription = "Capítulo Anterior", tint = Color.White, modifier = Modifier.size(30.dp))
            }
        }
        IconButton(onClick = onPrevious) {
            Icon(Icons.Default.SkipPrevious, contentDescription = "Anterior", tint = Color.White, modifier = Modifier.size(36.dp))
        }
        // Play / Pause keeps its larger visual circle without capping the
        // tappable area.
        IconButton(
            onClick = onPlayPause,
            modifier = Modifier
                .size(72.dp)
                .background(Color.White.copy(alpha = 0.2f), shape = androidx.compose.foundation.shape.CircleShape)
        ) {
            Icon(
                if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                contentDescription = if (isPlaying) "Pausar" else "Reproduzir",
                tint = Color.White,
                modifier = Modifier.size(48.dp)
            )
        }
        IconButton(onClick = onNext) {
            Icon(Icons.Default.SkipNext, contentDescription = "Próximo", tint = Color.White, modifier = Modifier.size(36.dp))
        }
        if (hasChapters) {
            IconButton(onClick = onNextChapter) {
                Icon(Icons.Default.FastForward, contentDescription = "Próximo Capítulo", tint = Color.White, modifier = Modifier.size(30.dp))
            }
        }
        IconButton(onClick = { onSeekBy(10_000L) }) {
            Icon(
                Icons.Default.Forward10,
                contentDescription = "Avançar 10 segundos",
                tint = Color.White,
                modifier = Modifier.size(34.dp),
            )
        }
    }
}

/**
 * Keeps the player actions reachable on narrow portrait windows and on devices
 * with large font scales. The title remains fixed while this action strip can
 * be explored horizontally.
 */
@Composable
internal fun PlayerTopBarActionsRow(
    modifier: Modifier = Modifier,
    content: @Composable RowScope.() -> Unit,
) {
    Row(
        modifier = modifier
            .horizontalScroll(rememberScrollState())
            .semantics {
                contentDescription = PLAYER_TOP_BAR_ACTIONS_CONTENT_DESCRIPTION
            },
        horizontalArrangement = Arrangement.End,
        verticalAlignment = Alignment.CenterVertically,
        content = content,
    )
}

// Helpers
@Composable
internal fun PlayerTrackMenu(
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
            Column(
                modifier = Modifier
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState()),
            ) {
                if (allowNone) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = selectedIndex == -1,
                                role = Role.RadioButton,
                                onClick = { onSelect(-1) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = selectedIndex == -1, onClick = null)
                        Text("Nenhuma", modifier = Modifier.padding(start = 8.dp))
                    }
                }
                tracks.forEachIndexed { index, track ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = selectedIndex == index,
                                role = Role.RadioButton,
                                onClick = { onSelect(index) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = selectedIndex == index, onClick = null)
                        Text(trackLabel(track), modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = {}
    )
}

@Composable
internal fun QualityMenu(
    qualities: List<String>,
    selectedQuality: String?,
    isMetered: Boolean = false,
    onSelect: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Qualidade") },
        text = {
            Column(
                modifier = Modifier
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState()),
            ) {
                if (isMetered) {
                    Text(
                        text = "Rede medida: o modo Auto limita a reprodução a 720p para reduzir o consumo de dados.",
                        style = MaterialTheme.typography.bodySmall,
                        modifier = Modifier.padding(bottom = 8.dp),
                    )
                }
                qualityMenuOptions(qualities).forEach { q ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = selectedQuality == q,
                                role = Role.RadioButton,
                                onClick = { onSelect(q) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = selectedQuality == q, onClick = null)
                        Text(qualityOptionLabel(q, isMetered), modifier = Modifier.padding(start = 8.dp))
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
            Column(
                modifier = Modifier
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState()),
            ) {
                speeds.forEach { speed ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = currentSpeed == speed,
                                role = Role.RadioButton,
                                onClick = { onSelect(speed) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = currentSpeed == speed, onClick = null)
                        Text("${speed}x", modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = {}
    )
}

@Composable
internal fun SleepTimerMenu(
    remainingMs: Long?,
    selectedMinutes: Int?,
    isAtMediaEnd: Boolean,
    onSelect: (Int?) -> Unit,
    onSelectAtMediaEnd: () -> Unit,
    onDismiss: () -> Unit,
) {
    val options = listOf(15, 30, 45, 60, 90)
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Temporizador de suspensão") },
        text = {
            Column(
                modifier = Modifier
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState()),
            ) {
                Text(
                    text = sleepTimerLabel(remainingMs) ?: "O player pausará automaticamente.",
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.padding(bottom = 8.dp),
                )
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier
                        .fillMaxWidth()
                        .selectable(
                            selected = remainingMs == null,
                            role = Role.RadioButton,
                            onClick = { onSelect(null) },
                        )
                        .padding(vertical = 8.dp),
                ) {
                    RadioButton(selected = remainingMs == null, onClick = null)
                    Text("Desativado", modifier = Modifier.padding(start = 8.dp))
                }
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier
                        .fillMaxWidth()
                        .selectable(
                            selected = isAtMediaEnd,
                            role = Role.RadioButton,
                            onClick = onSelectAtMediaEnd,
                        )
                        .padding(vertical = 8.dp),
                ) {
                    RadioButton(selected = isAtMediaEnd, onClick = null)
                    Text("Ao fim da mídia", modifier = Modifier.padding(start = 8.dp))
                }
                options.forEach { minutes ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                        .selectable(
                            selected = isSleepTimerOptionSelected(selectedMinutes, minutes),
                            role = Role.RadioButton,
                            onClick = { onSelect(minutes) },
                        )
                        .padding(vertical = 8.dp),
                    ) {
                    RadioButton(selected = isSleepTimerOptionSelected(selectedMinutes, minutes), onClick = null)
                        Text("${minutes} minutos", modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = {},
    )
}

@Composable
private fun AspectRatioMenu(
    currentRatio: VideoAspectRatio,
    onSelect: (VideoAspectRatio) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Proporção da Tela (Zoom)") },
        text = {
            Column(
                modifier = Modifier
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState()),
            ) {
                VideoAspectRatio.values().forEach { ratio ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = currentRatio == ratio,
                                role = Role.RadioButton,
                                onClick = { onSelect(ratio) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = currentRatio == ratio, onClick = null)
                        Text(ratio.title, modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = {}
    )
}

@Composable
internal fun PlaybackStatsDialog(
    title: String? = null,
    stats: PlaybackStats?,
    onCopy: () -> Unit,
    onShare: () -> Unit,
    onDismiss: () -> Unit,
) {
    var copied by remember { mutableStateOf(false) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Dados Técnicos da Mídia") },
        text = {
            Column(
                modifier = Modifier
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState())
                    .semantics {
                        contentDescription = PLAYBACK_STATS_CONTENT_DESCRIPTION
                    },
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                title?.takeIf(String::isNotBlank)?.let {
                    Text("Mídia: $it", style = MaterialTheme.typography.titleSmall)
                }
                Text("Método de Reprodução: ${stats?.playMethod ?: "Direct Play"}", style = MaterialTheme.typography.bodyMedium)
                stats?.resolution?.let { Text("Resolução: $it", style = MaterialTheme.typography.bodyMedium) }
                stats?.videoCodec?.let { Text("Codec de Vídeo: $it", style = MaterialTheme.typography.bodyMedium) }
                stats?.audioCodec?.let { Text("Codec de Áudio: $it", style = MaterialTheme.typography.bodyMedium) }
                stats?.bitrate?.let { Text("Taxa de Bits: $it", style = MaterialTheme.typography.bodyMedium) }
            }
        },
        confirmButton = {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                TextButton(onClick = {
                    onCopy()
                    copied = true
                }) {
                    Text(if (copied) "Copiado" else "Copiar")
                }
                TextButton(onClick = onShare) {
                    Text("Compartilhar")
                }
                TextButton(onClick = onDismiss) {
                    Text("Fechar")
                }
            }
        }
    )
}

private fun copyPlaybackStats(context: Context, title: String?, stats: PlaybackStats?) {
    val clipboard = context.getSystemService(ClipboardManager::class.java) ?: return
    clipboard.setPrimaryClip(ClipData.newPlainText("Dados técnicos da mídia", formatPlaybackStats(stats, title)))
}

private fun sharePlaybackStats(context: Context, title: String?, stats: PlaybackStats?) {
    val shareIntent = Intent(Intent.ACTION_SEND).apply {
        type = "text/plain"
        putExtra(Intent.EXTRA_SUBJECT, "Dados técnicos da mídia — MulletaFlix")
        putExtra(Intent.EXTRA_TEXT, formatPlaybackStats(stats, title))
        if (context !is Activity) addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    }
    context.startActivity(Intent.createChooser(shareIntent, "Compartilhar dados técnicos"))
}

// Extension: millis to time string
private fun Long.toTimeString(): String =
    org.mulletaflix.core.common.util.FormatUtils.formatDuration(this)
