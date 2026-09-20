package org.mulletaflix.feature.downloads

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import coil.compose.AsyncImage
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DownloadsScreen(
    onItemClick: (DownloadEntry) -> Unit,
    onBack: () -> Unit = {},
    onExploreClick: () -> Unit = {},
    viewModel: DownloadsViewModel = hiltViewModel()
) {
    val downloads by viewModel.downloads.collectAsStateWithLifecycle()
    val queuePaused by viewModel.queuePaused.collectAsStateWithLifecycle()
    val wifiOnly by viewModel.wifiOnly.collectAsStateWithLifecycle()
    val actionMessage by viewModel.actionMessage.collectAsStateWithLifecycle()
    val serverUrl = LocalMulletaFlixServerUrl.current
    val accessToken = LocalMulletaFlixAccessToken.current
    var searchQuery by rememberSaveable { mutableStateOf("") }
    var statusFilter by rememberSaveable { mutableStateOf(DownloadStatusFilter.All) }
    val filteredDownloads = remember(downloads, searchQuery, statusFilter) {
        filterDownloads(downloads, searchQuery, statusFilter)
    }
    var itemPendingDeletion by remember { mutableStateOf<DownloadEntry?>(null) }
    var showClearCompletedConfirmation by rememberSaveable { mutableStateOf(false) }
    var showClearFailedConfirmation by rememberSaveable { mutableStateOf(false) }
    var showStorageSummary by rememberSaveable { mutableStateOf(false) }

    val snackbarHostState = remember { SnackbarHostState() }
    LaunchedEffect(actionMessage) {
        actionMessage?.let { message ->
            snackbarHostState.showSnackbar(message)
            viewModel.clearActionMessage()
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Downloads Offline") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                    }
                },
                actions = {
                    IconButton(onClick = { showStorageSummary = true }) {
                        Icon(Icons.Default.Storage, contentDescription = "Ver armazenamento offline")
                    }
                }
            )
        }
    ) { padding ->
        Box(
            Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
        ) {
            if (downloads.isEmpty()) {
                EmptyDownloads(onExploreClick = onExploreClick)
            } else {
                LazyColumn(
                    Modifier.fillMaxSize(),
                    contentPadding = PaddingValues(16.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    item {
                        DownloadSearchField(
                            query = searchQuery,
                            onQueryChange = { searchQuery = it },
                            onClear = { searchQuery = "" },
                        )
                    }
                    item {
                        DownloadFilterRow(
                            selectedFilter = statusFilter,
                            onFilterSelected = { statusFilter = it },
                        )
                    }
                    item {
                        OfflineSummary(
                            downloads = downloads,
                            queuePaused = queuePaused,
                            onPause = viewModel::pauseQueue,
                            onResume = viewModel::resumeQueue,
                            onRetryFailed = { viewModel.retryFailed(downloads) },
                            onClearCompleted = { showClearCompletedConfirmation = true },
                            onClearFailed = { showClearFailedConfirmation = true },
                            wifiOnly = wifiOnly,
                            onWifiOnlyChange = viewModel::setWifiOnly,
                        )
                    }
                    if (filteredDownloads.isEmpty()) {
                        item {
                            Text(
                                text = emptyFilterMessage(searchQuery, statusFilter),
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 24.dp),
                                textAlign = androidx.compose.ui.text.style.TextAlign.Center,
                            )
                        }
                    }
                    items(filteredDownloads, key = { it.id }) { entry ->
                        DownloadRow(
                            entry = entry,
                            imageModel = resolveMediaUrl(serverUrl, entry.imageUrl, accessToken),
                            onPlay = { onItemClick(entry) },
                            onRetry = { viewModel.retry(entry) },
                            onRemove = { itemPendingDeletion = entry },
                        )
                    }
                }
            }
        }
    }

    itemPendingDeletion?.let { entry ->
        AlertDialog(
            onDismissRequest = { itemPendingDeletion = null },
            title = { Text("Excluir download?") },
            text = { Text("Deseja remover \"${entry.title}\" do armazenamento offline?") },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.remove(entry.id)
                        itemPendingDeletion = null
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error)
                ) {
                    Text("Excluir")
                }
            },
            dismissButton = {
                TextButton(onClick = { itemPendingDeletion = null }) {
                    Text("Cancelar")
                }
            }
        )
    }

    if (showClearCompletedConfirmation) {
        val completedCount = downloads.count { it.state == DownloadState.Completed }
        AlertDialog(
            onDismissRequest = { showClearCompletedConfirmation = false },
            title = { Text("Limpar concluídos?") },
            text = {
                Text(
                    "Remover $completedCount download(s) concluído(s) do armazenamento offline? " +
                        "Downloads em andamento e falhas serão preservados."
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.removeCompleted()
                        showClearCompletedConfirmation = false
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) { Text("Limpar") }
            },
            dismissButton = {
                TextButton(onClick = { showClearCompletedConfirmation = false }) { Text("Cancelar") }
            },
        )
    }

    if (showClearFailedConfirmation) {
        val failedCount = downloads.count { it.state == DownloadState.Failed }
        AlertDialog(
            onDismissRequest = { showClearFailedConfirmation = false },
            title = { Text("Limpar falhas?") },
            text = {
                Text(
                    "Remover $failedCount download(s) com falha da fila offline? " +
                        "Downloads em andamento e concluídos serão preservados."
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.removeFailed()
                        showClearFailedConfirmation = false
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) { Text("Limpar") }
            },
            dismissButton = {
                TextButton(onClick = { showClearFailedConfirmation = false }) { Text("Cancelar") }
            },
        )
    }

    if (showStorageSummary) {
        StorageSummaryDialog(
            summary = summarizeDownloadStorage(downloads),
            onDismiss = { showStorageSummary = false },
        )
    }
}

@Composable
internal fun StorageSummaryDialog(
    summary: DownloadStorageSummary,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Armazenamento offline") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("${summary.itemCount} item(ns) na fila offline.")
                Text("Dados baixados: ${formatStorageBytes(summary.downloadedBytes)}")
                if (summary.knownContentBytes > 0L) {
                    Text("Tamanho total estimado: ${formatStorageBytes(summary.knownContentBytes)}")
                } else {
                    Text(
                        "O tamanho total será informado pelo servidor quando estiver disponível.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Text(
                    "O valor considera o cache local do player.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("Fechar") }
        },
    )
}

@Composable
internal fun DownloadFilterRow(
    selectedFilter: DownloadStatusFilter,
    onFilterSelected: (DownloadStatusFilter) -> Unit,
) {
    Column(Modifier.fillMaxWidth()) {
        Text(
            text = "Filtrar por status",
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(8.dp))
        LazyRow(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            contentPadding = PaddingValues(horizontal = 2.dp),
        ) {
            items(DownloadStatusFilter.entries.size) { index ->
                val filter = DownloadStatusFilter.entries[index]
                FilterChip(
                    selected = filter == selectedFilter,
                    onClick = { onFilterSelected(filter) },
                    label = { Text(filter.label) },
                )
            }
        }
    }
}

private fun emptyFilterMessage(query: String, filter: DownloadStatusFilter): String {
    val normalizedQuery = query.trim()
    return when {
        normalizedQuery.isNotEmpty() && filter != DownloadStatusFilter.All ->
            "Nenhum download encontrado para \"$normalizedQuery\" em ${filter.label.lowercase()}."
        normalizedQuery.isNotEmpty() -> "Nenhum download encontrado para \"$normalizedQuery\"."
        filter != DownloadStatusFilter.All -> "Nenhum download ${filter.label.lowercase()}."
        else -> "Nenhum download encontrado."
    }
}

@Composable
internal fun DownloadSearchField(
    query: String,
    onQueryChange: (String) -> Unit,
    onClear: () -> Unit,
) {
    OutlinedTextField(
        value = query,
        onValueChange = onQueryChange,
        modifier = Modifier
            .fillMaxWidth()
            .padding(bottom = 12.dp),
        singleLine = true,
        label = { Text("Buscar downloads") },
        placeholder = { Text("Digite o nome do título") },
        leadingIcon = {
            Icon(Icons.Default.Search, contentDescription = "Buscar downloads")
        },
        trailingIcon = if (query.isNotEmpty()) {
            {
                IconButton(onClick = onClear) {
                    Icon(Icons.Default.Clear, contentDescription = "Limpar busca")
                }
            }
        } else {
            null
        },
    )
}

@Composable
internal fun OfflineSummary(
    downloads: List<DownloadEntry>,
    queuePaused: Boolean,
    onPause: () -> Unit,
    onResume: () -> Unit,
    onRetryFailed: () -> Unit,
    onClearCompleted: () -> Unit,
    onClearFailed: () -> Unit,
    wifiOnly: Boolean,
    onWifiOnlyChange: (Boolean) -> Unit,
) {
    val hasActiveDownloads = downloads.any { it.state == DownloadState.Queued || it.state == DownloadState.Downloading }
    val failedCount = failedDownloads(downloads).size
    val completedCount = completedDownloads(downloads).size
    Card(
        Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
    ) {
        Column(Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.CloudDone, null, tint = MaterialTheme.colorScheme.secondary)
                Spacer(Modifier.width(12.dp))
                Column(Modifier.weight(1f)) {
                    Text("Modo offline", style = MaterialTheme.typography.titleSmall)
                    Text(
                        "${downloads.count { it.state == DownloadState.Completed }} concluído(s) • ${downloads.size} na fila",
                        style = MaterialTheme.typography.bodySmall
                    )
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
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.Wifi, null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.width(12.dp))
                Text("Somente Wi‑Fi", Modifier.weight(1f), style = MaterialTheme.typography.bodyMedium)
                Switch(checked = wifiOnly, onCheckedChange = onWifiOnlyChange)
            }
            if (failedCount > 0) {
                OutlinedButton(
                    onClick = onRetryFailed,
                    modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                ) {
                    Icon(Icons.Default.Refresh, contentDescription = null)
                    Spacer(Modifier.width(8.dp))
                    Text("Tentar novamente ($failedCount falha(s))")
                }
            }
            if (completedCount > 0) {
                TextButton(
                    onClick = onClearCompleted,
                    modifier = Modifier.fillMaxWidth().padding(top = 4.dp),
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) {
                    Icon(Icons.Default.DeleteSweep, contentDescription = null)
                    Spacer(Modifier.width(8.dp))
                    Text("Limpar concluídos ($completedCount)")
                }
            }
            if (failedCount > 0) {
                TextButton(
                    onClick = onClearFailed,
                    modifier = Modifier.fillMaxWidth().padding(top = 4.dp),
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) {
                    Icon(Icons.Default.DeleteSweep, contentDescription = null)
                    Spacer(Modifier.width(8.dp))
                    Text("Limpar falhas ($failedCount)")
                }
            }
        }
    }
}

@Composable
private fun DownloadRow(
    entry: DownloadEntry,
    imageModel: String?,
    onPlay: () -> Unit,
    onRetry: () -> Unit,
    onRemove: () -> Unit
) {
    Card(
        Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
            if (imageModel != null) {
                AsyncImage(
                    model = imageModel,
                    contentDescription = entry.title,
                    contentScale = offlineArtworkContentScale(),
                    modifier = Modifier.size(width = 56.dp, height = 80.dp),
                )
            } else {
                Icon(
                    if (entry.state == DownloadState.Completed) Icons.Default.DownloadDone else Icons.Default.Downloading,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.secondary,
                    modifier = Modifier.size(32.dp)
                )
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(entry.title, style = MaterialTheme.typography.titleMedium)
                Text(
                    statusText(entry),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                if (entry.state == DownloadState.Downloading || entry.state == DownloadState.Queued) {
                    LinearProgressIndicator(
                        progress = { entry.percent / 100f },
                        modifier = Modifier.fillMaxWidth().padding(top = 6.dp)
                    )
                }
            }
            if (entry.state == DownloadState.Completed) {
                IconButton(onClick = onPlay) {
                    Icon(Icons.Default.PlayArrow, "Reproduzir offline")
                }
            }
            if (entry.state == DownloadState.Failed) {
                IconButton(onClick = onRetry) {
                    Icon(Icons.Default.Refresh, "Tentar download novamente")
                }
            }
            IconButton(onClick = onRemove) {
                Icon(Icons.Default.Delete, "Remover", tint = MaterialTheme.colorScheme.error)
            }
        }
    }
}

private fun statusText(entry: DownloadEntry) = when (entry.state) {
    DownloadState.Completed -> "Disponível offline"
    DownloadState.Downloading -> "Baixando… ${entry.percent}%"
    DownloadState.Queued -> "Aguardando conexão"
    DownloadState.Removing -> "Removendo…"
    DownloadState.Failed -> entry.error ?: "Falha no download"
}

@Composable
private fun EmptyDownloads(onExploreClick: () -> Unit) {
    Column(
        Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Icon(Icons.Default.DownloadDone, null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(16.dp))
        Text("Nenhum download concluído", style = MaterialTheme.typography.titleMedium)
        Text(
            "Os títulos baixados para assistir sem internet aparecerão aqui.",
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp, bottom = 16.dp),
            style = MaterialTheme.typography.bodyMedium
        )
        Button(onClick = onExploreClick) {
            Icon(Icons.Default.Explore, contentDescription = null, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(8.dp))
            Text("Explorar Catálogo")
        }
    }
}
