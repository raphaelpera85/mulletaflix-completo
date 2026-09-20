package org.mulletaflix.feature.search

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.speech.RecognitionListener
import android.speech.RecognizerIntent
import android.speech.SpeechRecognizer
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.platform.LocalContext
import androidx.core.content.ContextCompat
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.domain.model.*

/**
 * Universal search screen.
 *
 * - Search field with debounce 350ms
 * - Results grouped by type: Movies, Series, Episodes, Music, Albums, Artists, People
 * - Search history (with individual remove and instant replay) shown when field is empty
 * - Filter chips by content type
 * - Error feedback with retry action
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SearchScreen(
    onItemClick: (String) -> Unit,
    onBack: () -> Unit = {},
    viewModel: SearchViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    var isListening by remember { mutableStateOf(false) }
    var voiceError by remember { mutableStateOf<String?>(null) }
    var showClearHistoryConfirmation by rememberSaveable { mutableStateOf(false) }
    val speechRecognizer = remember(context) {
        runCatching {
            if (SpeechRecognizer.isRecognitionAvailable(context)) {
                SpeechRecognizer.createSpeechRecognizer(context)
            } else null
        }.getOrNull()
    }
    DisposableEffect(speechRecognizer) {
        onDispose { speechRecognizer?.destroy() }
    }
    DisposableEffect(speechRecognizer, viewModel) {
        val listener = object : RecognitionListener {
            override fun onReadyForSpeech(params: Bundle?) { isListening = true }
            override fun onBeginningOfSpeech() = Unit
            override fun onRmsChanged(rmsdB: Float) = Unit
            override fun onBufferReceived(buffer: ByteArray?) = Unit
            override fun onEndOfSpeech() { isListening = false }
            override fun onError(error: Int) {
                isListening = false
                voiceError = voiceSearchErrorMessage(error)
            }
            override fun onResults(results: Bundle?) {
                isListening = false
                val query = recognizedVoiceQuery(
                    results?.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION),
                )
                if (query != null) viewModel.search(query)
                else voiceError = voiceSearchErrorMessage(SpeechRecognizer.ERROR_NO_MATCH)
            }
            override fun onPartialResults(partialResults: Bundle?) = Unit
            override fun onEvent(eventType: Int, params: Bundle?) = Unit
        }
        speechRecognizer?.setRecognitionListener(listener)
        onDispose { speechRecognizer?.setRecognitionListener(null) }
    }
    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission(),
    ) { granted ->
        if (granted) beginVoiceSearch(context, speechRecognizer)
        else voiceError = voiceSearchErrorMessage(SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS)
    }

    Column(modifier = Modifier.fillMaxSize()) {

        // ── Search field ───────────────────────────────────────────────────
        SearchBar(
            inputField = {
                SearchBarDefaults.InputField(
                    query = state.query,
                    onQueryChange = viewModel::onQueryChange,
                    onSearch = viewModel::search,
                    expanded = false,
                    onExpandedChange = {},
                    placeholder = { Text("Buscar filmes, séries, músicas...") },
                    leadingIcon = {
                        IconButton(onClick = onBack) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                        }
                    },
                    trailingIcon = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            if (speechRecognizer != null) {
                                IconButton(
                                    onClick = {
                                        voiceError = null
                                        if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
                                            beginVoiceSearch(context, speechRecognizer)
                                        } else {
                                            permissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
                                        }
                                    },
                                    enabled = !isListening,
                                ) {
                                    if (isListening) {
                                        CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                                    } else {
                                        Icon(Icons.Default.Mic, contentDescription = "Buscar por voz")
                                    }
                                }
                            }
                            if (state.query.isNotEmpty()) {
                                IconButton(onClick = { viewModel.onQueryChange("") }) {
                                    Icon(Icons.Default.Close, contentDescription = "Limpar")
                                }
                            }
                        }
                    },
                )
            },
            expanded = false,
            onExpandedChange = {},
            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
            content = {}
        )

        voiceError?.let { message ->
            Text(
                text = message,
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.bodySmall,
                modifier = Modifier.padding(horizontal = 20.dp, vertical = 2.dp),
            )
        }

        // ── Filter chips ────────────────────────────────────────────────────
        LazyRow(
            contentPadding = PaddingValues(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.padding(bottom = 8.dp)
        ) {
            item {
                FilterChip(
                    selected = state.activeFilter == null,
                    onClick = { viewModel.setFilter(null) },
                    label = { Text("Tudo") }
                )
            }
            items(SearchFilter.values().toList()) { filter ->
                FilterChip(
                    selected = state.activeFilter == filter,
                    onClick = { viewModel.setFilter(filter) },
                    label = { Text(filter.label) }
                )
            }
        }

        // ── Results ─────────────────────────────────────────────────────────
        if (state.isLoading) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
        } else if (state.error != null && state.results.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize().padding(16.dp), contentAlignment = Alignment.Center) {
                Card(
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                    modifier = Modifier.fillMaxWidth().padding(16.dp)
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text(
                            text = state.error ?: "Erro ao buscar",
                            color = MaterialTheme.colorScheme.onErrorContainer,
                            style = MaterialTheme.typography.bodyMedium
                        )
                        Button(onClick = viewModel::retrySearch) {
                            Text("Tentar novamente")
                        }
                    }
                }
            }
        } else if (state.query.isBlank()) {
            // Show search history
            SearchHistory(
                history = state.history,
                onItemClick = viewModel::search,
                onRemoveItem = viewModel::removeHistoryItem,
                onClearHistory = { showClearHistoryConfirmation = true },
            )
        } else if (state.results.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(Icons.Default.SearchOff, contentDescription = null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text("Nenhum resultado para \"${state.query}\"", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 8.dp))
                }
            }
        } else {
            // Grouped results
            PullToRefreshBox(
                isRefreshing = state.isRefreshing,
                onRefresh = viewModel::refreshSearch,
                modifier = Modifier.fillMaxSize(),
            ) {
            LazyColumn {
                if (state.error != null) {
                    item {
                        Card(
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
                        ) {
                            Row(
                                modifier = Modifier.padding(start = 12.dp, end = 4.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Text(
                                    text = "Não foi possível atualizar a busca.",
                                    color = MaterialTheme.colorScheme.onErrorContainer,
                                    modifier = Modifier.weight(1f),
                                )
                                TextButton(onClick = viewModel::retrySearch) { Text("Tentar") }
                            }
                        }
                    }
                }
                val grouped = state.results.groupBy { it.type.toGroupLabel() }
                grouped.forEach { (groupLabel, items) ->
                    item {
                        Text(
                            text = groupLabel,
                            style = MaterialTheme.typography.titleSmall,
                            color = MaterialTheme.colorScheme.secondary,
                            modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
                        )
                    }
                    item {
                        LazyRow(
                            contentPadding = PaddingValues(horizontal = 16.dp),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            modifier = Modifier.padding(bottom = 8.dp)
                        ) {
                            items(items) { item ->
                                val cardShape = if (item.type.usesPosterArtwork()) {
                                    MediaCardShape.Portrait
                                } else {
                                    MediaCardShape.Landscape
                                }
                                MediaCard(
                                    title = item.name,
                                    imageUrl = item.primaryImageUrl,
                                    metadata = item.cardMetadata(),
                                    shape = cardShape,
                                    isWatched = item.isPlayed,
                                    onClick = { onItemClick(item.id) },
                                    modifier = Modifier.width(if (cardShape == MediaCardShape.Portrait) 110.dp else 190.dp)
                                )
                            }
                        }
                    }
                }
                item { Spacer(modifier = Modifier.height(80.dp)) }
            }
            }
        }
    }

    if (showClearHistoryConfirmation) {
        ClearSearchHistoryDialog(
            onConfirm = {
                viewModel.clearHistory()
                showClearHistoryConfirmation = false
            },
            onDismiss = { showClearHistoryConfirmation = false },
        )
    }
}

@Composable
internal fun ClearSearchHistoryDialog(
    onConfirm: () -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Limpar histórico?") },
        text = { Text("Todas as buscas recentes serão removidas deste usuário.") },
        confirmButton = {
            TextButton(
                onClick = onConfirm,
                colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
            ) { Text("Limpar") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancelar") }
        },
    )
}

private fun beginVoiceSearch(context: Context, recognizer: SpeechRecognizer?) {
    recognizer ?: return
    val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
        putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
        putExtra(RecognizerIntent.EXTRA_LANGUAGE, "pt-BR")
        putExtra(RecognizerIntent.EXTRA_PROMPT, "Diga o nome do filme ou série")
    }
    runCatching { recognizer.startListening(intent) }
        .onFailure { /* RecognitionListener reports the normal provider errors. */ }
}

@Composable
private fun SearchHistory(
    history: List<String>,
    onItemClick: (String) -> Unit,
    onRemoveItem: (String) -> Unit,
    onClearHistory: () -> Unit,
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(bottom = 80.dp),
    ) {
        item {
            Row(
                modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("Buscas Recentes", style = MaterialTheme.typography.titleSmall)
                if (history.isNotEmpty()) {
                    TextButton(onClick = onClearHistory) { Text("Limpar") }
                }
            }
        }
        if (history.isEmpty()) {
            item {
                Text(
                    "Suas buscas recentes aparecerão aqui.",
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        } else {
            items(history, key = { it }) { query ->
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clickable { onItemClick(query) }
                        .padding(horizontal = 16.dp, vertical = 6.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Icon(Icons.Default.History, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(query, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f).padding(horizontal = 12.dp))
                    IconButton(onClick = { onRemoveItem(query) }) {
                        Icon(
                            Icons.Default.Close,
                            contentDescription = "Remover da busca",
                            tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(18.dp),
                        )
                    }
                }
            }
        }
    }
}

enum class SearchFilter(val label: String) {
    Movies("Filmes"),
    Series("Séries"),
    Episodes("Episódios"),
    Music("Músicas"),
    Albums("Álbuns"),
    Artists("Artistas"),
    People("Pessoas"),
}

private fun MediaItemType.toGroupLabel() = when (this) {
    MediaItemType.Movie, MediaItemType.Trailer -> "Filmes"
    MediaItemType.Series -> "Séries"
    MediaItemType.Season, MediaItemType.Episode -> "Episódios"
    MediaItemType.Audio -> "Músicas"
    MediaItemType.MusicAlbum -> "Álbuns"
    MediaItemType.MusicArtist -> "Artistas"
    MediaItemType.Book -> "Livros"
    else -> "Outros"
}
