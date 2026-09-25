package org.mulletaflix.feature.syncplay

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CastConnected
import androidx.compose.material.icons.filled.FastForward
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Stop
import androidx.compose.material.icons.filled.Replay
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.progressBarRangeInfo
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.designsystem.components.remoteFocusRing
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackSession
import org.mulletaflix.core.common.util.FormatUtils
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import androidx.lifecycle.repeatOnLifecycle

@Composable
@OptIn(ExperimentalMaterial3Api::class)
fun RemotePlaybackScreen(
    onBack: () -> Unit,
    viewModel: RemotePlaybackViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var stopTarget by remember { mutableStateOf<String?>(null) }
    val lifecycleOwner = LocalLifecycleOwner.current
    LaunchedEffect(Unit) { viewModel.refresh() }
    LaunchedEffect(stopTarget, state.sessions) {
        if (stopTarget != null && state.sessions.none { it.id == stopTarget }) {
            stopTarget = null
        }
    }
    LaunchedEffect(lifecycleOwner) {
        lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.RESUMED) {
            while (isActive) {
                delay(REMOTE_SESSIONS_REFRESH_INTERVAL_MILLIS)
                viewModel.refresh()
            }
        }
    }

    Scaffold(topBar = {
        TopAppBar(
            title = { Text("Dispositivos em reprodução") },
            navigationIcon = {
                MulletaFlixTopBarAction(onClick = onBack) {
                    Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                }
            },
            actions = {
                MulletaFlixTopBarAction(onClick = viewModel::refresh, busy = state.isLoading) {
                    Icon(Icons.Default.Refresh, contentDescription = "Atualizar dispositivos")
                }
            },
        )
    }) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize().padding(padding),
            contentPadding = androidx.compose.foundation.layout.PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            item {
                Text(
                    "Controle a reprodução em TVs e outros dispositivos conectados à sua conta.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            if (state.isLoading && state.sessions.isEmpty()) {
                item { CircularProgressIndicator(Modifier.padding(24.dp)) }
            }
            state.error?.let { message ->
                item {
                    Text(message, color = MaterialTheme.colorScheme.error)
                    TextButton(onClick = viewModel::refresh) { Text("Tentar novamente") }
                }
            }
            if (!state.isLoading && state.error == null && state.sessions.isEmpty()) {
                item {
                    Column(
                        modifier = Modifier.fillMaxWidth().padding(vertical = 36.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Icon(Icons.Default.CastConnected, contentDescription = null, modifier = Modifier.size(40.dp))
                        Text("Nenhuma reprodução ativa", style = MaterialTheme.typography.titleMedium)
                        Text("Inicie um vídeo em uma TV ou outro dispositivo e atualize esta tela.", style = MaterialTheme.typography.bodyMedium)
                    }
                }
            }
            items(state.sessions, key = { it.id }) { session ->
                RemotePlaybackSessionCard(
                    session = session,
                    busy = state.busySessionId != null,
                    onToggle = { viewModel.sendCommand(session.id, RemotePlaybackCommand.PLAY_PAUSE) },
                    onSeek = { ticks -> viewModel.sendCommand(session.id, RemotePlaybackCommand.SEEK, ticks) },
                    onStop = { stopTarget = session.id },
                )
            }
            state.notice?.let { notice -> item { Text(notice, color = MaterialTheme.colorScheme.primary) } }
        }
    }

    if (stopTarget != null) {
        AlertDialog(
            onDismissRequest = { stopTarget = null },
            title = { Text("Parar reprodução?") },
            text = { Text("A reprodução será encerrada no dispositivo selecionado.") },
            confirmButton = {
                Button(onClick = {
                    val target = stopTarget
                    if (target != null && state.sessions.any { it.id == target }) {
                        viewModel.sendCommand(target, RemotePlaybackCommand.STOP)
                    }
                    stopTarget = null
                }) { Text("Parar") }
            },
            dismissButton = { TextButton(onClick = { stopTarget = null }) { Text("Cancelar") } },
        )
    }
}

@Composable
internal fun RemotePlaybackSessionCard(
    session: RemotePlaybackSession,
    busy: Boolean,
    onToggle: () -> Unit,
    onSeek: (Long) -> Unit,
    onStop: () -> Unit,
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(session.itemName, style = MaterialTheme.typography.titleMedium)
            Text("${session.deviceName} • ${session.clientName} • ${if (session.isPaused) "Pausado" else "Reproduzindo"}", style = MaterialTheme.typography.bodyMedium)
            val durationTicks = session.durationTicks?.takeIf { it > 0L }
            if (durationTicks != null) {
                val positionTicks = session.positionTicks.coerceIn(0L, durationTicks)
                val progress = (positionTicks.toDouble() / durationTicks.toDouble()).toFloat()
                val elapsed = FormatUtils.formatDuration(FormatUtils.ticksToMillis(positionTicks))
                val total = FormatUtils.formatDuration(FormatUtils.ticksToMillis(durationTicks))
                LinearProgressIndicator(
                    progress = { progress },
                    modifier = Modifier.fillMaxWidth().semantics {
                        contentDescription = "Progresso da reprodução: $elapsed de $total"
                        progressBarRangeInfo = ProgressBarRangeInfo(progress, 0f..1f)
                    },
                )
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(elapsed, style = MaterialTheme.typography.labelMedium)
                    Text(total, style = MaterialTheme.typography.labelMedium)
                }
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                Button(
                    onClick = onToggle,
                    enabled = !busy,
                    modifier = Modifier.weight(1f).remoteFocusRing(),
                ) {
                    Icon(if (session.isPaused) Icons.Default.PlayArrow else Icons.Default.Pause, contentDescription = null)
                    Spacer(Modifier.width(6.dp))
                    Text(if (session.isPaused) "Retomar" else "Pausar")
                }
                if (session.canSeek) {
                    OutlinedButton(onClick = {
                        onSeek(remoteSeekTargetTicks(session.positionTicks, session.durationTicks, -REMOTE_SEEK_TICKS))
                    }, enabled = !busy, modifier = Modifier.remoteFocusRing().semantics { contentDescription = "Voltar 30 segundos" }) {
                        Icon(Icons.Default.Replay, contentDescription = null)
                    }
                    OutlinedButton(onClick = {
                        onSeek(remoteSeekTargetTicks(session.positionTicks, session.durationTicks, REMOTE_SEEK_TICKS))
                    }, enabled = !busy, modifier = Modifier.remoteFocusRing().semantics { contentDescription = "Avançar 30 segundos" }) {
                        Icon(Icons.Default.FastForward, contentDescription = null)
                    }
                }
                OutlinedButton(onClick = onStop, enabled = !busy, modifier = Modifier.remoteFocusRing().semantics { contentDescription = "Parar reprodução" }) {
                    Icon(Icons.Default.Stop, contentDescription = null)
                }
            }
        }
    }
}

private const val REMOTE_SEEK_TICKS = 30L * 10_000_000L
internal const val REMOTE_SESSIONS_REFRESH_INTERVAL_MILLIS = 30_000L
