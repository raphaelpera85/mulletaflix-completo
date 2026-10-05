package org.mulletaflix.feature.itemdetail

import android.app.Application
import androidx.compose.foundation.background
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.snapshotFlow
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import org.readium.navigator.web.reflowable.ReflowableWebConfiguration
import org.readium.navigator.web.reflowable.ReflowableWebRendition
import org.readium.navigator.web.reflowable.ReflowableWebRenditionFactory
import org.readium.navigator.web.reflowable.ReflowableWebRenditionState
import org.readium.navigator.web.reflowable.preferences.ReflowableWebPreferences
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.launch
import kotlinx.coroutines.CancellationException
import org.readium.navigator.web.reflowable.ReflowableWebGoLocation
import kotlin.math.roundToInt

@Composable
@OptIn(ExperimentalMaterial3Api::class)
fun BookReaderScreen(
    itemId: String,
    onBack: () -> Unit,
    viewModel: BookReaderViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val comicArchive = state.comicArchive
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }
    var currentComicPage by rememberSaveable(itemId) { mutableIntStateOf(0) }
    var comicZoom by rememberSaveable(itemId) { mutableFloatStateOf(1f) }
    var renditionState by remember(itemId) { mutableStateOf<ReflowableWebRenditionState?>(null) }
    var renditionError by remember(itemId) { mutableStateOf<String?>(null) }

    LaunchedEffect(itemId) { viewModel.load(itemId) }
    LaunchedEffect(state.publication) {
        renditionState = null
        renditionError = null
        val publication = state.publication ?: return@LaunchedEffect
        runCatching {
            ReflowableWebRenditionFactory(
                application = context.applicationContext as Application,
                publication = publication,
                configuration = ReflowableWebConfiguration(),
            )?.createRenditionState(
                initialPreferences = ReflowableWebPreferences(
                    fontSize = state.fontSizePercent / 100.0,
                ),
                initialLocation = state.initialLocator?.let(::ReflowableWebGoLocation),
            )?.getOrNull() ?: error("Não foi possível preparar a leitura.")
        }.onSuccess { renditionState = it }
            .onFailure { renditionError = "Não foi possível renderizar este livro." }
    }
    val renditionController = renditionState?.controller
    LaunchedEffect(state.bookmarkMessage) {
        val message = state.bookmarkMessage ?: return@LaunchedEffect
        snackbarHostState.showSnackbar(message)
        viewModel.clearBookmarkMessage()
    }
    LaunchedEffect(renditionController, state.fontSizePercent) {
        val controller = renditionController ?: return@LaunchedEffect
        val fontSize = state.fontSizePercent / 100.0
        if (controller.preferences.fontSize != fontSize) {
            controller.preferences = controller.preferences.copy(fontSize = fontSize)
        }
    }
    LaunchedEffect(itemId, renditionController) {
        val controller = renditionController ?: return@LaunchedEffect
        snapshotFlow { controller.location.toLocator() }
            .distinctUntilChanged()
            .collect { locator -> viewModel.saveReadingProgression(itemId, locator) }
    }
    LaunchedEffect(itemId, state.comicArchive, state.initialLocator) {
        val archive = state.comicArchive ?: return@LaunchedEffect
        currentComicPage = ComicBookArchive.pageIndexFromLocator(state.initialLocator, archive.pageCount) ?: 0
    }
    LaunchedEffect(currentComicPage) { comicZoom = 1f }
    LaunchedEffect(itemId, state.comicArchive) {
        val archive = state.comicArchive ?: return@LaunchedEffect
                snapshotFlow { currentComicPage }
            .distinctUntilChanged()
            .collect { page ->
                viewModel.saveReadingProgression(
                    itemId,
                    archive.locatorForPage(page),
                )
            }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Leitor de livros", maxLines = 1) },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                    }
                },
                actions = {
                    if (comicArchive != null && !state.isLoading) {
                        ComicBookZoomControls(
                            zoom = comicZoom,
                            onZoomChange = { comicZoom = normalizeComicPageZoom(it) },
                        )
                    }
                    key(itemId) {
                        BookReaderProgressActions(
                            enabled = (state.publication != null || state.comicArchive != null) && !state.isLoading,
                            bookmarks = state.bookmarks,
                            bookmarkLabelSuggestion = comicArchive?.let { "Página ${currentComicPage + 1}" }
                                ?: renditionController?.location?.toLocator()?.locations?.totalProgression?.let { progression ->
                                    "Leitura ${(progression * 100).roundToInt()}%"
                                }
                                ?: "Posição salva",
                            onSaveBookmark = { label ->
                                val locator = comicArchive?.locatorForPage(currentComicPage)
                                    ?: renditionController?.location?.toLocator()
                                if (locator != null) {
                                    viewModel.saveBookmark(itemId, locator, label)
                                }
                            },
                            onOpenBookmark = { bookmark ->
                                if (comicArchive != null) {
                                    ComicBookArchive.pageIndexFromLocator(bookmark.locator, comicArchive.pageCount)
                                        ?.let { currentComicPage = it }
                                        ?: viewModel.showBookmarkMessage("Este marcador não existe mais neste livro.")
                                } else {
                                    renditionController?.let { controller ->
                                        coroutineScope.launch {
                                            try {
                                                controller.goTo(ReflowableWebGoLocation(bookmark.locator))
                                            } catch (cancelled: CancellationException) {
                                                throw cancelled
                                            } catch (_: Exception) {
                                                viewModel.showBookmarkMessage("Não foi possível abrir este marcador.")
                                            }
                                        }
                                    }
                                }
                            },
                            onDeleteBookmark = { bookmarkId -> viewModel.deleteBookmark(itemId, bookmarkId) },
                            onRenameBookmark = { bookmarkId, label -> viewModel.renameBookmark(itemId, bookmarkId, label) },
                            onRestart = { viewModel.restartReadingFromBeginning(itemId) },
                        )
                    }
                },
            )
        },
        bottomBar = {
            val archive = state.comicArchive
            if (archive != null) {
                ComicBookPageControls(
                    currentPage = currentComicPage,
                    pageCount = archive.pageCount,
                    onPageSelected = { page -> currentComicPage = page.coerceIn(0, archive.pageCount - 1) },
                )
            } else {
                val controller = renditionState?.controller
                Row(
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    IconButton(onClick = { controller?.let { nav -> coroutineScope.launch { nav.moveBackward() } } }, enabled = controller != null) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Página anterior")
                    }
                    BookReaderFontSizeControls(
                        fontSizePercent = state.fontSizePercent,
                        enabled = controller != null,
                        onDecrease = {
                            viewModel.setFontSizePercent(
                                itemId,
                                BookReaderFontSize.decrease(state.fontSizePercent),
                            )
                        },
                        onIncrease = {
                            viewModel.setFontSizePercent(
                                itemId,
                                BookReaderFontSize.increase(state.fontSizePercent),
                            )
                        },
                    )
                    IconButton(onClick = { controller?.let { nav -> coroutineScope.launch { nav.moveForward() } } }, enabled = controller != null) {
                        Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = "Próxima página")
                    }
                }
            }
        },
    ) { contentPadding ->
        Box(
            modifier = Modifier.fillMaxSize().padding(contentPadding)
                .background(MaterialTheme.colorScheme.background),
            contentAlignment = Alignment.Center,
        ) {
            when {
                state.isLoading -> CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
                state.error != null -> ReaderMessage(
                    message = state.error.orEmpty(),
                    onRetry = { viewModel.load(itemId) },
                )
                renditionError != null -> ReaderMessage(
                    message = renditionError.orEmpty(),
                    onRetry = { viewModel.load(itemId) },
                )
                comicArchive != null -> ComicBookReaderContent(
                    archive = comicArchive,
                    currentPage = currentComicPage,
                    zoom = comicZoom,
                    onZoomChange = { comicZoom = it },
                    modifier = Modifier.fillMaxSize(),
                )
                renditionState != null -> ReflowableWebRendition(
                    state = renditionState!!,
                    modifier = Modifier.fillMaxSize(),
                )
                else -> CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
            }
        }
    }
}

@Composable
internal fun BookReaderFontSizeControls(
    fontSizePercent: Int,
    enabled: Boolean,
    onDecrease: () -> Unit,
    onIncrease: () -> Unit,
) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        IconButton(
            onClick = onDecrease,
            enabled = enabled && fontSizePercent > BookReaderFontSize.MIN_PERCENT,
            modifier = Modifier.semantics(mergeDescendants = true) {
                contentDescription = "Diminuir tamanho do texto"
            },
        ) {
            Icon(Icons.Default.Remove, contentDescription = null)
        }
        Text(
            text = "${BookReaderFontSize.normalize(fontSizePercent)}%",
            style = MaterialTheme.typography.labelLarge,
            modifier = Modifier.padding(horizontal = 4.dp),
        )
        IconButton(
            onClick = onIncrease,
            enabled = enabled && fontSizePercent < BookReaderFontSize.MAX_PERCENT,
            modifier = Modifier.semantics(mergeDescendants = true) {
                contentDescription = "Aumentar tamanho do texto"
            },
        ) {
            Icon(Icons.Default.Add, contentDescription = null)
        }
    }
}

@Composable
@OptIn(ExperimentalMaterial3Api::class)
internal fun BookReaderProgressActions(
    enabled: Boolean,
    bookmarks: List<BookReaderBookmark>,
    bookmarkLabelSuggestion: String,
    onSaveBookmark: (String) -> Unit,
    onOpenBookmark: (BookReaderBookmark) -> Unit,
    onDeleteBookmark: (String) -> Unit,
    onRenameBookmark: (String, String) -> Unit,
    onRestart: () -> Unit,
) {
    var menuExpanded by remember { mutableStateOf(false) }
    var confirmationVisible by remember { mutableStateOf(false) }
    var bookmarksVisible by remember { mutableStateOf(false) }
    var bookmarkNameDialogVisible by remember { mutableStateOf(false) }
    var bookmarkName by rememberSaveable { mutableStateOf("") }
    var bookmarkBeingRenamed by remember { mutableStateOf<BookReaderBookmark?>(null) }

    Box {
        IconButton(
            onClick = { menuExpanded = true },
            enabled = enabled,
        ) {
            Icon(Icons.Default.MoreVert, contentDescription = "Mais opções de leitura")
        }
        DropdownMenu(
            expanded = menuExpanded,
            onDismissRequest = { menuExpanded = false },
        ) {
            DropdownMenuItem(
                text = { Text("Salvar posição atual") },
                enabled = bookmarks.size < BookReaderProgressStore.MAX_BOOKMARKS_PER_BOOK,
                onClick = {
                    menuExpanded = false
                    bookmarkName = ""
                    bookmarkNameDialogVisible = true
                },
            )
            DropdownMenuItem(
                text = { Text("Marcadores salvos (${bookmarks.size})") },
                onClick = {
                    menuExpanded = false
                    bookmarksVisible = true
                },
            )
            DropdownMenuItem(
                text = { Text("Reiniciar do começo") },
                onClick = {
                    menuExpanded = false
                    confirmationVisible = true
                },
            )
        }
    }

    if (bookmarkNameDialogVisible) {
        BookmarkNameDialog(
            title = if (bookmarkBeingRenamed == null) "Nome do marcador" else "Renomear marcador",
            fieldLabel = if (bookmarkBeingRenamed == null) "Nome personalizado (opcional)" else "Novo nome",
            name = bookmarkName,
            suggestion = bookmarkLabelSuggestion,
            onNameChange = { bookmarkName = it.take(BookReaderProgressStore.MAX_BOOKMARK_LABEL_LENGTH) },
            onDismiss = {
                bookmarkNameDialogVisible = false
                if (bookmarkBeingRenamed != null) bookmarksVisible = true
                bookmarkBeingRenamed = null
            },
            onSave = {
                val bookmark = bookmarkBeingRenamed
                if (bookmark == null) {
                    onSaveBookmark(bookmarkName.trim().ifBlank { bookmarkLabelSuggestion })
                } else {
                    onRenameBookmark(bookmark.id, bookmarkName)
                    bookmarksVisible = true
                }
                bookmarkNameDialogVisible = false
                bookmarkBeingRenamed = null
            },
            allowBlank = bookmarkBeingRenamed == null,
        )
    }

    if (bookmarksVisible) {
        AlertDialog(
            onDismissRequest = { bookmarksVisible = false },
            title = { Text("Marcadores de leitura") },
            text = {
                if (bookmarks.isEmpty()) {
                    Text("Nenhum marcador salvo neste livro.")
                } else {
                    LazyColumn(modifier = Modifier.heightIn(max = 360.dp)) {
                        items(bookmarks, key = BookReaderBookmark::id) { bookmark ->
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                TextButton(
                                    onClick = {
                                        bookmarksVisible = false
                                        onOpenBookmark(bookmark)
                                    },
                                    modifier = Modifier.weight(1f),
                                ) {
                                    Text(bookmark.label, maxLines = 2)
                                }
                                IconButton(
                                    onClick = {
                                        bookmarkBeingRenamed = bookmark
                                        bookmarkName = bookmark.label
                                        bookmarksVisible = false
                                        bookmarkNameDialogVisible = true
                                    },
                                    modifier = Modifier.semantics(mergeDescendants = true) {
                                        contentDescription = "Renomear marcador ${bookmark.label}"
                                    },
                                ) {
                                    Icon(Icons.Default.Edit, contentDescription = null)
                                }
                                IconButton(
                                    onClick = { onDeleteBookmark(bookmark.id) },
                                    modifier = Modifier.semantics(mergeDescendants = true) {
                                        contentDescription = "Excluir marcador ${bookmark.label}"
                                    },
                                ) {
                                    Icon(Icons.Default.Delete, contentDescription = null)
                                }
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { bookmarksVisible = false }) { Text("Fechar") }
            },
        )
    }

    if (confirmationVisible) {
        AlertDialog(
            onDismissRequest = { confirmationVisible = false },
            title = { Text("Reiniciar leitura?") },
            text = { Text("A posição salva deste livro será apagada e a leitura voltará ao começo.") },
            confirmButton = {
                TextButton(onClick = {
                    confirmationVisible = false
                    onRestart()
                }) { Text("Reiniciar") }
            },
            dismissButton = {
                TextButton(onClick = { confirmationVisible = false }) { Text("Cancelar") }
            },
        )
    }
}

@Composable
private fun BookmarkNameDialog(
    title: String,
    fieldLabel: String,
    name: String,
    suggestion: String,
    onNameChange: (String) -> Unit,
    onDismiss: () -> Unit,
    onSave: () -> Unit,
    allowBlank: Boolean,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = {
            OutlinedTextField(
                value = name,
                onValueChange = onNameChange,
                modifier = Modifier.fillMaxWidth().testTag(BOOK_READER_BOOKMARK_NAME_TEST_TAG),
                singleLine = true,
                label = { Text(fieldLabel) },
                placeholder = { Text(suggestion) },
                supportingText = {
                    Text(
                        if (name.isBlank()) "Vazio usa: $suggestion"
                        else "${name.length}/${BookReaderProgressStore.MAX_BOOKMARK_LABEL_LENGTH}",
                    )
                },
            )
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } },
        confirmButton = {
            TextButton(onClick = onSave, enabled = allowBlank || name.isNotBlank()) { Text("Salvar") }
        },
    )
}

internal const val BOOK_READER_BOOKMARK_NAME_TEST_TAG = "book-reader-bookmark-name"

@Composable
private fun ReaderMessage(message: String, onRetry: () -> Unit) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(message, textAlign = TextAlign.Center, modifier = Modifier.padding(24.dp))
        IconButton(onClick = onRetry) {
            Icon(Icons.Default.Refresh, contentDescription = "Tentar carregar o livro novamente")
        }
    }
}
