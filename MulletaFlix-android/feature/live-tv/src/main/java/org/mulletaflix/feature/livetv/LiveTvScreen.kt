package org.mulletaflix.feature.livetv

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import org.mulletaflix.domain.model.MediaItem

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LiveTvScreen(onChannelPlay: (String) -> Unit, viewModel: LiveTvViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var showGuide by remember { mutableStateOf(false) }
    Scaffold(topBar = { TopAppBar(title = { Text("TV Ao Vivo & EPG") }, actions = { IconButton(onClick = viewModel::refresh, enabled = !state.isLoading) { Icon(Icons.Default.Refresh, "Atualizar canais") }; IconButton(onClick = { showGuide = true; viewModel.loadGuide() }, enabled = state.channels.isNotEmpty() && !state.isLoadingGuide) { Icon(Icons.Default.CalendarMonth, "Guia EPG") } }) }) { padding ->
        LazyColumn(Modifier.fillMaxSize().padding(padding).background(MaterialTheme.colorScheme.background), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            item { Text("Canais disponíveis", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.secondary, modifier = Modifier.padding(bottom = 8.dp)) }
            state.error?.let { error -> item { Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer), modifier = Modifier.fillMaxWidth()) { Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) { Text(error, Modifier.weight(1f), color = MaterialTheme.colorScheme.onErrorContainer); TextButton(onClick = viewModel::refresh) { Text("Tentar novamente") } } } } }
            if (state.isLoading && state.channels.isEmpty()) item { Box(Modifier.fillMaxWidth().padding(32.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() } }
            if (!state.isLoading && state.channels.isEmpty() && state.error == null) item { EmptyLiveTvState() }
            items(state.channels, key = { it.id }) { channel -> ChannelRow(channel, onPlay = { onChannelPlay(channel.id) }) }
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
                    RecordingRow(recording, onPlay = { onChannelPlay(recording.id) })
                }
            }
        }
    }
    if (showGuide) AlertDialog(onDismissRequest = { showGuide = false }, title = { Text("Guia das próximas 24 horas") }, text = { GuideContent(state, onSchedule = viewModel::scheduleRecording) }, confirmButton = { TextButton(onClick = { showGuide = false }) { Text("Fechar") } })
}

@Composable
private fun ChannelRow(channel: MediaItem, onPlay: () -> Unit) { Card(Modifier.fillMaxWidth().clickable(onClick = onPlay), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)) { Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) { Surface(Modifier.size(48.dp), shape = MaterialTheme.shapes.small, color = MaterialTheme.colorScheme.primaryContainer) { Box(contentAlignment = Alignment.Center) { Icon(Icons.Default.LiveTv, null, tint = MaterialTheme.colorScheme.onPrimaryContainer) } }; Spacer(Modifier.width(16.dp)); Column(Modifier.weight(1f)) { Text(channel.name, style = MaterialTheme.typography.titleSmall); Text(channel.overview ?: "Sem informações de guia", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }; IconButton(onClick = onPlay) { Icon(Icons.Default.PlayCircleOutline, "Assistir", tint = MaterialTheme.colorScheme.secondary) } } } }

@Composable
private fun RecordingRow(recording: MediaItem, onPlay: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onPlay),
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
                recording.startDate?.let { date ->
                    Text(date.replace('T', ' ').removeSuffix("Z"), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
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
private fun GuideContent(state: LiveTvUiState, onSchedule: (MediaItem) -> Unit) {
    when {
        state.isLoadingGuide -> Box(Modifier.fillMaxWidth().padding(24.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
        state.guideError != null -> Text(state.guideError)
        state.programs.isEmpty() -> Text("Nenhum programa encontrado para as próximas 24 horas.")
        else -> Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            state.programs.forEach { program ->
                Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
                    Row(Modifier.fillMaxWidth().padding(10.dp), verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(program.name, style = MaterialTheme.typography.bodyMedium)
                            program.overview?.let { Text(it, maxLines = 2, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                        }
                        val canSchedule = !program.channelId.isNullOrBlank() && !program.startDate.isNullOrBlank() && !program.endDate.isNullOrBlank()
                        if (canSchedule) {
                            val scheduled = program.id in state.scheduledProgramIds
                            TextButton(onClick = { onSchedule(program) }, enabled = !scheduled && state.schedulingProgramId != program.id) {
                                Text(if (scheduled) "Agendado" else if (state.schedulingProgramId == program.id) "Agendando…" else "Gravar")
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun EmptyLiveTvState() { Column(Modifier.fillMaxWidth().padding(vertical = 32.dp), horizontalAlignment = Alignment.CenterHorizontally) { Icon(Icons.Default.LiveTv, null, modifier = Modifier.size(48.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant); Spacer(Modifier.height(8.dp)); Text("Nenhum canal disponível", style = MaterialTheme.typography.titleMedium); Text("O servidor não retornou canais de TV ao vivo.", color = MaterialTheme.colorScheme.onSurfaceVariant) } }
