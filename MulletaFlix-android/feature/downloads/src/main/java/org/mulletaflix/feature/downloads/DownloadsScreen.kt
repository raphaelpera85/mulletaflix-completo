package org.mulletaflix.feature.downloads

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DownloadsScreen(onItemClick: (DownloadEntry) -> Unit, viewModel: DownloadsViewModel = hiltViewModel()) {
    val downloads by viewModel.downloads.collectAsStateWithLifecycle()
    val queuePaused by viewModel.queuePaused.collectAsStateWithLifecycle()
    Scaffold(topBar = { TopAppBar(title = { Text("Downloads Offline") }, actions = { Icon(Icons.Default.Storage, "Armazenamento", modifier = Modifier.padding(end = 16.dp)) }) }) { padding ->
        Box(Modifier.fillMaxSize().padding(padding).background(MaterialTheme.colorScheme.background)) {
            if (downloads.isEmpty()) EmptyDownloads()
            else LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                item { OfflineSummary(downloads, queuePaused, onPause = viewModel::pauseQueue, onResume = viewModel::resumeQueue) }
                items(downloads, key = { it.id }) { entry ->
                    DownloadRow(
                        entry,
                        onPlay = { onItemClick(entry) },
                        onRetry = { viewModel.retry(entry) },
                        onRemove = { viewModel.remove(entry.id) },
                    )
                }
            }
        }
    }
}

@Composable private fun OfflineSummary(
    downloads: List<DownloadEntry>,
    queuePaused: Boolean,
    onPause: () -> Unit,
    onResume: () -> Unit,
) {
    val hasActiveDownloads = downloads.any { it.state == DownloadState.Queued || it.state == DownloadState.Downloading }
    Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
        Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Default.CloudDone, null, tint = MaterialTheme.colorScheme.secondary)
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text("Modo offline", style = MaterialTheme.typography.titleSmall)
                Text("${downloads.count { it.state == DownloadState.Completed }} concluído(s) • ${downloads.size} na fila", style = MaterialTheme.typography.bodySmall)
            }
            if (hasActiveDownloads) {
                IconButton(onClick = if (queuePaused) onResume else onPause) {
                    Icon(
                        if (queuePaused) Icons.Default.PlayArrow else Icons.Default.Pause,
                        contentDescription = if (queuePaused) "Retomar downloads" else "Pausar downloads",
                    )
                }
            }
        }
    }
}

@Composable private fun DownloadRow(entry: DownloadEntry, onPlay: () -> Unit, onRetry: () -> Unit, onRemove: () -> Unit) { Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)) { Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) { Icon(if (entry.state == DownloadState.Completed) Icons.Default.DownloadDone else Icons.Default.Downloading, null, tint = MaterialTheme.colorScheme.secondary, modifier = Modifier.size(32.dp)); Spacer(Modifier.width(12.dp)); Column(Modifier.weight(1f)) { Text(entry.title, style = MaterialTheme.typography.titleMedium); Text(statusText(entry), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant); if (entry.state == DownloadState.Downloading || entry.state == DownloadState.Queued) LinearProgressIndicator(progress = { entry.percent / 100f }, modifier = Modifier.fillMaxWidth().padding(top = 6.dp)) }; if (entry.state == DownloadState.Completed) IconButton(onClick = onPlay) { Icon(Icons.Default.PlayArrow, "Reproduzir offline") }; if (entry.state == DownloadState.Failed) IconButton(onClick = onRetry) { Icon(Icons.Default.Refresh, "Tentar download novamente") }; IconButton(onClick = onRemove) { Icon(Icons.Default.Delete, "Remover", tint = MaterialTheme.colorScheme.error) } } } }

private fun statusText(entry: DownloadEntry) = when (entry.state) { DownloadState.Completed -> "Disponível offline"; DownloadState.Downloading -> "Baixando… ${entry.percent}%"; DownloadState.Queued -> "Aguardando conexão"; DownloadState.Removing -> "Removendo…"; DownloadState.Failed -> entry.error ?: "Falha no download" }

@Composable private fun EmptyDownloads() { Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.Center, horizontalAlignment = Alignment.CenterHorizontally) { Icon(Icons.Default.DownloadDone, null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant); Spacer(Modifier.height(16.dp)); Text("Nenhum download concluído", style = MaterialTheme.typography.titleMedium); Text("Os downloads iniciados no player aparecerão aqui.", color = MaterialTheme.colorScheme.onSurfaceVariant) } }
