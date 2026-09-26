package org.mulletaflix.feature.livetv

import androidx.compose.foundation.background
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.clickable
import androidx.compose.foundation.border
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.draw.scale
import androidx.compose.ui.platform.LocalConfiguration
import android.content.res.Configuration
import androidx.compose.ui.Alignment
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import coil.compose.AsyncImagePainter
import coil.compose.SubcomposeAsyncImage
import coil.compose.SubcomposeAsyncImageContent
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.theme.MulletaFlixRed
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.primaryImageUrl
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LiveTvScreen(
    onChannelPlay: (String) -> Unit,
    onBack: () -> Unit = {},
    viewModel: LiveTvViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val lifecycleOwner = LocalLifecycleOwner.current
    val isTelevision = (LocalConfiguration.current.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
        Configuration.UI_MODE_TYPE_TELEVISION
    var showGuide by remember { mutableStateOf(false) }

    LiveTvRefreshEffect(
        lifecycleOwner = lifecycleOwner,
        refreshIntervalMillis = liveTvAutoRefreshIntervalMillis(isTelevision),
        refreshImmediately = refreshLiveTvImmediatelyOnResume(isTelevision),
        onRefresh = viewModel::refreshIfIdle,
    )
    LiveTvRefreshEffect(
        lifecycleOwner = lifecycleOwner,
        refreshIntervalMillis = if (shouldRefreshLiveTvGuide(showGuide)) {
            LIVE_TV_GUIDE_REFRESH_INTERVAL_MILLIS
        } else {
            0L
        },
        refreshImmediately = refreshLiveTvGuideImmediatelyOnResume(showGuide, isTelevision),
        onRefresh = viewModel::loadGuide,
    )
    Scaffold(
        topBar = {
            LiveTvTopBar(
                onBack = onBack,
                onRefresh = viewModel::refresh,
                onGuide = {
                    showGuide = true
                    if (isTelevision) viewModel.loadGuide()
                },
                isLoading = state.isLoading,
                isLoadingGuide = state.isLoadingGuide,
                hasChannels = state.channels.isNotEmpty(),
            )
        },
    ) { padding ->
        LazyColumn(Modifier.fillMaxSize().padding(padding).background(MaterialTheme.colorScheme.background), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            item { Text("Canais disponíveis", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.secondary, modifier = Modifier.padding(bottom = 8.dp)) }
            if (state.isOffline) {
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Row(
                            modifier = Modifier.padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Icon(Icons.Default.CloudOff, contentDescription = null, tint = MaterialTheme.colorScheme.secondary)
                            Text(
                                "Sem conexão. Os canais serão atualizados automaticamente quando a rede voltar.",
                                style = MaterialTheme.typography.bodySmall,
                            )
                        }
                    }
                }
            }
            state.error?.let { error -> item { Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer), modifier = Modifier.fillMaxWidth()) { Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) { Text(error, Modifier.weight(1f), color = MaterialTheme.colorScheme.onErrorContainer); GuideActionButton(isTelevision = isTelevision, onClick = viewModel::refresh) { Text("Tentar novamente") } } } } }
            if (state.isLoading && state.channels.isEmpty()) item { Box(Modifier.fillMaxWidth().padding(32.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() } }
            if (!state.isLoading && state.channels.isEmpty() && state.error == null) item { EmptyLiveTvState() }
            items(state.channels, key = { it.id }) { channel ->
                ChannelRow(channel, isTelevision = isTelevision, onPlay = { onChannelPlay(channel.id) })
            }
            // A falha das gravações não pode sumir com a seção: sem esta linha, um erro
            // de rede era indistinguível de "você não tem gravações".
            state.recordingsError?.let { message ->
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                        modifier = Modifier.fillMaxWidth().padding(top = 12.dp),
                    ) {
                        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                            Text(message, Modifier.weight(1f), color = MaterialTheme.colorScheme.onErrorContainer)
                            GuideActionButton(isTelevision = isTelevision, onClick = viewModel::refresh) { Text("Tentar novamente") }
                        }
                    }
                }
            }
            if (state.recordings.isNotEmpty()) {
                item {
                    Text(
                        "Gravações",
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.secondary,
                        modifier = Modifier.padding(top = 12.dp, bottom = 2.dp),
                    )
                }
                items(state.recordings, key = { "recording-${it.id}" }) { recording ->
                    RecordingRow(recording, isTelevision = isTelevision, onPlay = { onChannelPlay(recording.id) })
                }
            }
        }
    }
    if (showGuide) AlertDialog(
        onDismissRequest = { showGuide = false; viewModel.closeGuide() },
        title = { Text("Guia das próximas 24 horas") },
        text = {
            GuideContent(
                state = state,
                isTelevision = isTelevision,
                onSchedule = viewModel::scheduleRecording,
                onCancel = viewModel::cancelScheduledRecording,
                onRetry = viewModel::loadGuide,
                onRetryTimerLookup = viewModel::retryScheduledRecordingTimerLookup,
            )
        },
        confirmButton = {
            GuideActionButton(isTelevision = isTelevision, onClick = { showGuide = false; viewModel.closeGuide() }) {
                Text("Fechar")
            }
        },
    )
}

/**
 * Top bar kept separate from the screen so TV remote actions can be verified
 * without constructing a Hilt ViewModel or making a network request.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun LiveTvTopBar(
    onBack: () -> Unit,
    onRefresh: () -> Unit,
    onGuide: () -> Unit,
    isLoading: Boolean,
    isLoadingGuide: Boolean,
    hasChannels: Boolean,
) {
    TopAppBar(
        title = { Text("TV Ao Vivo & EPG") },
        navigationIcon = {
            MulletaFlixTopBarAction(onClick = onBack) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
            }
        },
        actions = {
            MulletaFlixTopBarAction(
                onClick = onRefresh,
                busy = isLoading,
                busyContentDescription = "Atualizar canais",
            ) {
                Icon(Icons.Default.Refresh, "Atualizar canais")
            }
            MulletaFlixTopBarAction(
                onClick = onGuide,
                enabled = hasChannels,
                busy = isLoadingGuide,
                busyContentDescription = "Guia EPG",
            ) {
                Icon(Icons.Default.CalendarMonth, "Guia EPG")
            }
        },
    )
}

@Composable
private fun ChannelRow(channel: MediaItem, isTelevision: Boolean, onPlay: () -> Unit) {
    var isFocused by remember { mutableStateOf(false) }
    val focusScale by animateFloatAsState(
        targetValue = if (isTelevision && isFocused) 1.03f else 1f,
        label = "live-tv-channel-focus-scale",
    )
    val imageUrl = resolveMediaUrl(
        LocalMulletaFlixServerUrl.current,
        channel.primaryImageUrl,
        LocalMulletaFlixAccessToken.current,
    )
    Card(
        Modifier
            .fillMaxWidth()
            .scale(focusScale)
            .then(
                if (isTelevision) {
                    // No `focusable()`: the `clickable` below already provides a
                    // focus target, and a second one on the same node swallowed the
                    // remote's first press, so these rows needed two clicks to play.
                    Modifier.onFocusChanged { isFocused = it.isFocused }
                } else Modifier
            )
            .then(
                if (isTelevision && isFocused) {
                    Modifier.border(2.dp, MulletaFlixRed, MaterialTheme.shapes.medium)
                } else Modifier
            )
            .clickable(onClick = onPlay),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
    ) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            ChannelLogo(channel.name, imageUrl)
            Spacer(Modifier.width(16.dp))
            Column(Modifier.weight(1f)) {
                Text(channel.name, style = MaterialTheme.typography.titleSmall)
                Text(
                    channel.overview ?: "Sem informações de guia",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                )
            }
            MulletaFlixTopBarAction(onClick = onPlay) {
                Icon(Icons.Default.PlayCircleOutline, "Assistir ${channel.name}", tint = MaterialTheme.colorScheme.secondary)
            }
        }
    }
}

@Composable
private fun ChannelLogo(channelName: String, imageUrl: String?) {
    Surface(
        Modifier.size(56.dp),
        shape = MaterialTheme.shapes.small,
        color = MaterialTheme.colorScheme.primaryContainer,
    ) {
        SubcomposeAsyncImage(
            model = imageUrl,
            contentDescription = "Logo do canal $channelName",
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize(),
        ) {
            when (painter.state) {
                is AsyncImagePainter.State.Success -> SubcomposeAsyncImageContent()
                else -> Box(contentAlignment = Alignment.Center) {
                    Icon(
                        Icons.Default.LiveTv,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onPrimaryContainer,
                    )
                }
            }
        }
    }
}

@Composable
private fun RecordingRow(recording: MediaItem, isTelevision: Boolean, onPlay: () -> Unit) {
    var isFocused by remember { mutableStateOf(false) }
    val focusScale by animateFloatAsState(
        targetValue = if (isTelevision && isFocused) 1.03f else 1f,
        label = "live-tv-recording-focus-scale",
    )
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .scale(focusScale)
            .then(
                if (isTelevision) {
                    // No `focusable()`: the `clickable` below already provides a
                    // focus target, and a second one on the same node swallowed the
                    // remote's first press, so these rows needed two clicks to play.
                    Modifier.onFocusChanged { isFocused = it.isFocused }
                } else Modifier
            )
            .then(
                if (isTelevision && isFocused) {
                    Modifier.border(2.dp, MulletaFlixRed, MaterialTheme.shapes.medium)
                } else Modifier
            )
            .clickable(onClick = onPlay),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
    ) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Surface(Modifier.size(48.dp), shape = MaterialTheme.shapes.small, color = MaterialTheme.colorScheme.secondaryContainer) {
                Box(contentAlignment = Alignment.Center) {
                    Icon(Icons.Default.FiberManualRecord, null, tint = MaterialTheme.colorScheme.onSecondaryContainer)
                }
            }
            Spacer(Modifier.width(16.dp))
            Column(Modifier.weight(1f)) {
                Text(recording.name, style = MaterialTheme.typography.titleSmall)
                recordingStartLabel(recording.startDate)?.let { label ->
                    Text(label, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                recording.overview?.takeIf(String::isNotBlank)?.let { overview ->
                    Text(overview, maxLines = 2, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            Icon(Icons.Default.PlayCircleOutline, "Reproduzir gravação", tint = MaterialTheme.colorScheme.secondary)
        }
    }
}

@Composable
internal fun GuideContent(
    state: LiveTvUiState,
    isTelevision: Boolean = false,
    onSchedule: (MediaItem) -> Unit,
    onCancel: (MediaItem) -> Unit,
    onRetry: () -> Unit,
    onRetryTimerLookup: (MediaItem) -> Unit,
) {
    Column {
        // The error is a banner, not a replacement. It used to be rendered
        // *instead of* the programmes, so one failed refresh discarded a guide
        // the user was already reading and the dialog offered no way to retry.
        state.guideError?.let { message ->
            Row(
                modifier = Modifier.fillMaxWidth().padding(bottom = 8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = message,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodySmall,
                    modifier = Modifier.weight(1f),
                )
                GuideActionButton(isTelevision = isTelevision, onClick = onRetry) {
                    Text("Tentar novamente")
                }
            }
        }
        state.recordingActionError?.let { message ->
            Text(
                text = message,
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodySmall,
                modifier = Modifier.fillMaxWidth().padding(bottom = 8.dp),
            )
        }
        when (guideBody(state.isLoadingGuide, state.programs.size)) {
            GuideBody.LOADING -> Box(Modifier.fillMaxWidth().padding(24.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            GuideBody.EMPTY -> Text("Nenhum programa encontrado para as próximas 24 horas.")
            GuideBody.PROGRAMMES -> LazyColumn(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 420.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                items(state.programs, key = { it.id }) { program ->
                    Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
                        Row(Modifier.fillMaxWidth().padding(10.dp), verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text(program.name, style = MaterialTheme.typography.bodyMedium)
                                program.overview?.let { Text(it, maxLines = 2, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                            }
                            val canSchedule = !program.channelId.isNullOrBlank() && !program.startDate.isNullOrBlank() && !program.endDate.isNullOrBlank()
                            if (canSchedule) {
                                val scheduled = program.id in state.scheduledProgramIds
                                val scheduling = program.id in state.schedulingProgramIds
                                if (scheduled) {
                                    Column(horizontalAlignment = Alignment.End) {
                                        Text("Agendado", style = MaterialTheme.typography.labelMedium)
                                        if (program.id in state.scheduledProgramTimerIds) {
                                            val cancelling = program.id in state.cancellingProgramIds
                                            GuideActionButton(
                                                isTelevision = isTelevision,
                                                onClick = { onCancel(program) },
                                                enabled = !cancelling && !state.isOffline,
                                            ) {
                                                Text(if (cancelling) "Cancelando…" else "Cancelar")
                                            }
                                        } else {
                                            val resolvingTimer = program.id in state.resolvingTimerProgramIds
                                            Text(
                                                if (resolvingTimer) "Confirmando com o servidor…"
                                                else "Aguardando confirmação do servidor",
                                                style = MaterialTheme.typography.labelSmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                            )
                                            GuideActionButton(
                                                isTelevision = isTelevision,
                                                onClick = { onRetryTimerLookup(program) },
                                                enabled = !resolvingTimer && !state.isOffline,
                                            ) {
                                                Text(if (resolvingTimer) "Verificando…" else "Verificar")
                                            }
                                        }
                                    }
                                } else {
                                    GuideActionButton(
                                        isTelevision = isTelevision,
                                        onClick = { onSchedule(program) },
                                        enabled = !scheduling && !state.isOffline,
                                    ) {
                                        Text(if (scheduling) "Agendando…" else "Gravar")
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun GuideActionButton(
    isTelevision: Boolean,
    onClick: () -> Unit,
    enabled: Boolean = true,
    content: @Composable RowScope.() -> Unit,
) {
    var isFocused by remember { mutableStateOf(false) }
    TextButton(
        onClick = onClick,
        enabled = enabled,
        modifier = Modifier
            .onFocusChanged { isFocused = it.isFocused }
            .then(
                if (isTelevision && isFocused) {
                    Modifier.border(2.dp, MulletaFlixRed, MaterialTheme.shapes.small)
                } else {
                    Modifier
                },
            ),
        content = content,
    )
}

@Composable
private fun EmptyLiveTvState() { Column(Modifier.fillMaxWidth().padding(vertical = 32.dp), horizontalAlignment = Alignment.CenterHorizontally) { Icon(Icons.Default.LiveTv, null, modifier = Modifier.size(48.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant); Spacer(Modifier.height(8.dp)); Text("Nenhum canal disponível", style = MaterialTheme.typography.titleMedium); Text("O servidor não retornou canais de TV ao vivo.", color = MaterialTheme.colorScheme.onSurfaceVariant) } }
