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
import androidx.compose.foundation.border
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import android.content.res.Configuration
import androidx.core.content.ContextCompat
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.domain.model.*

/** Tag do controle de "carregar mais", para o teste medir o estado do botão. */
internal const val LOAD_MORE_TEST_TAG = "search-load-more"

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
    val isTelevision = (LocalConfiguration.current.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
        Configuration.UI_MODE_TYPE_TELEVISION
    var isListening by remember { mutableStateOf(false) }
    var voiceError by remember { mutableStateOf<String?>(null) }
    var showClearHistoryConfirmation by rememberSaveable { mutableStateOf(false) }
    val resultsScrollState = rememberSearchScrollState()
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

    BoxWithConstraints(modifier = Modifier.fillMaxSize()) {
        val contentMaxWidth = searchContentMaxWidthDp(maxWidth.value.toInt(), isTelevision).dp
        Column(
            modifier = Modifier
                .fillMaxSize()
                .widthIn(max = contentMaxWidth)
                .align(Alignment.TopCenter),
        ) {

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
                        MulletaFlixTopBarAction(onClick = onBack) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                        }
                    },
                    trailingIcon = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            if (speechRecognizer != null) {
                                MulletaFlixTopBarAction(
                                    onClick = {
                                        voiceError = null
                                        if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
                                            beginVoiceSearch(context, speechRecognizer)
                                        } else {
                                            permissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
                                        }
                                    },
                                    // `busy`, não `enabled`: o microfone está trabalhando,
                                    // não indisponível. Com `enabled = false` o indicador
                                    // ficava a 38% de opacidade e o botão perdia o nome
                                    // acessível — o contrato do componente reserva `enabled`
                                    // para "não há no que agir".
                                    busy = isListening,
                                    busyContentDescription = "Ouvindo…",
                                ) {
                                    // O indicador de "ouvindo" é o do próprio componente
                                    // quando `busy`, e ele mantém o nome acessível.
                                    Icon(Icons.Default.Mic, contentDescription = "Buscar por voz")
                                }
                            }
                            if (state.query.isNotEmpty()) {
                                MulletaFlixTopBarAction(onClick = { viewModel.onQueryChange("") }) {
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

        if (state.hints.isNotEmpty() || state.isLoadingHints) {
            SearchHintPanel(
                hints = state.hints,
                isLoading = state.isLoadingHints,
                onHintClick = { hint ->
                    viewModel.search(hint.name)
                    onItemClick(hint.id)
                },
                focusFriendly = isTelevision,
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

        if (state.isOffline) {
            Card(
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.errorContainer,
                ),
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 4.dp),
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Icon(
                        Icons.Default.CloudOff,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onErrorContainer,
                    )
                    Text(
                        text = "Sem conexão. A busca será atualizada quando a rede voltar.",
                        color = MaterialTheme.colorScheme.onErrorContainer,
                        style = MaterialTheme.typography.bodySmall,
                        modifier = Modifier.padding(start = 8.dp),
                    )
                }
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
                focusFriendly = isTelevision,
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
            LazyColumn(state = resultsScrollState) {
                searchTruncationNotice(state.results.size, state.totalMatching)?.let { notice ->
                    item { SearchTruncationBanner(notice) }
                }
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
                                    text = if (state.canRetryLoadMore) {
                                        "Não foi possível carregar mais resultados."
                                    } else {
                                        "Não foi possível atualizar a busca."
                                    },
                                    color = MaterialTheme.colorScheme.onErrorContainer,
                                    modifier = Modifier.weight(1f),
                                )
                                TextButton(
                                    onClick = if (state.canRetryLoadMore) viewModel::retryLoadMore else viewModel::retrySearch,
                                ) { Text("Tentar") }
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
                        val carouselScrollState = rememberSearchCarouselScrollState()
                        LazyRow(
                            state = carouselScrollState,
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
                                    focusFriendly = isTelevision,
                                    onClick = { onItemClick(item.id) },
                                    modifier = Modifier.width(if (cardShape == MediaCardShape.Portrait) 110.dp else 190.dp)
                                )
                            }
                        }
                    }
                }
                if (state.hasMore || state.isLoadingMore) {
                    item {
                        LoadMoreRow(
                            isLoading = state.isLoadingMore,
                            onLoadMore = viewModel::loadMore,
                        )
                    }
                }
                item { Spacer(modifier = Modifier.height(80.dp)) }
            }
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

/**
 * O controle de "carregar mais", no fim da lista.
 *
 * A busca mostrava 30 de 412 e a única saída era "refine a busca" — o que é um conselho
 * ruim para quem sabe o que procura. Isto é o resto da resposta.
 *
 * Enquanto carrega, o botão vira um indicador **no lugar dele**, e não uma tela cheia de
 * spinner: o que já foi lido precisa continuar visível, com a posição de rolagem.
 */
@Composable
internal fun LoadMoreRow(
    isLoading: Boolean,
    onLoadMore: () -> Unit,
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 16.dp),
        contentAlignment = Alignment.Center,
    ) {
        if (isLoading) {
            CircularProgressIndicator(
                modifier = Modifier
                    .size(32.dp)
                    .testTag(LOAD_MORE_TEST_TAG),
            )
        } else {
            Button(
                onClick = onLoadMore,
                modifier = Modifier.testTag(LOAD_MORE_TEST_TAG),
            ) {
                Text("Carregar mais")
            }
        }
    }
}

/**
 * Linha que admite que a lista está incompleta.
 *
 * Recebe a frase pronta de [searchTruncationNotice] em vez de recalcular aqui: quem
 * decide se há truncamento (e se o servidor contou alguma coisa) é a política, que é
 * testável sem Compose.
 */
@Composable
internal fun SearchTruncationBanner(notice: String) {
    Text(
        text = notice,
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
    )
}

@Composable
internal fun SearchHintPanel(
    hints: List<org.mulletaflix.domain.repository.SearchHintItem>,
    isLoading: Boolean,
    onHintClick: (org.mulletaflix.domain.repository.SearchHintItem) -> Unit,
    focusFriendly: Boolean,
    state: LazyListState = rememberSearchScrollState(),
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
    ) {
        if (isLoading && hints.isEmpty()) {
            LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
        }
        LazyColumn(
            state = state,
            modifier = Modifier.heightIn(max = 280.dp),
            contentPadding = PaddingValues(vertical = 4.dp),
        ) {
            items(hints, key = { it.id }) { hint ->
                var isFocused by remember { mutableStateOf(false) }
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .onFocusChanged { isFocused = it.isFocused }
                        .clickable(onClick = { onHintClick(hint) })
                        .semantics {
                            role = Role.Button
                            contentDescription = "Abrir sugestão ${hint.name}"
                        }
                        .then(
                            if (focusFriendly && isFocused) {
                                Modifier.border(2.dp, MaterialTheme.colorScheme.primary, MaterialTheme.shapes.small)
                            } else Modifier
                        )
                        .padding(horizontal = 16.dp, vertical = 12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Icon(Icons.Default.Search, contentDescription = null, tint = MaterialTheme.colorScheme.secondary)
                    Column(modifier = Modifier.padding(start = 12.dp)) {
                        Text(hint.name, style = MaterialTheme.typography.bodyLarge)
                        val detail = listOfNotNull(hint.type, hint.year?.toString()).joinToString(" • ")
                        if (detail.isNotBlank()) Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }
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
internal fun SearchHistory(
    history: List<String>,
    onItemClick: (String) -> Unit,
    onRemoveItem: (String) -> Unit,
    onClearHistory: () -> Unit,
    focusFriendly: Boolean = false,
    state: LazyListState = rememberSearchScrollState(),
) {
    LazyColumn(
        state = state,
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
                var isFocused by remember { mutableStateOf(false) }
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .then(
                            if (focusFriendly) {
                                Modifier
                                    .onFocusChanged { isFocused = it.isFocused }
                                    // No `focusable()`: `clickable` already provides
                                    // a focus target, and a second one on the same
                                    // node swallowed the remote's first press (the
                                    // row needed two clicks to activate).
                                    .clickable { onItemClick(query) }
                                    .semantics {
                                        role = Role.Button
                                        contentDescription = "Pesquisar novamente por $query"
                                    }
                            } else {
                                Modifier.clickable { onItemClick(query) }
                            },
                        )
                        .then(
                            if (focusFriendly && isFocused) {
                                Modifier.border(2.dp, MaterialTheme.colorScheme.primary, MaterialTheme.shapes.medium)
                            } else {
                                Modifier
                            },
                        )
                        .padding(horizontal = 16.dp, vertical = 6.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Icon(Icons.Default.History, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(query, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f).padding(horizontal = 12.dp))
                    MulletaFlixTopBarAction(onClick = { onRemoveItem(query) }) {
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
