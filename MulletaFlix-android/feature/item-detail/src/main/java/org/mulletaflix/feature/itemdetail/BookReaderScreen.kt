package org.mulletaflix.feature.itemdetail

import android.app.Application
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
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
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.snapshotFlow
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
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
import org.readium.navigator.web.reflowable.ReflowableWebGoLocation

@Composable
@OptIn(ExperimentalMaterial3Api::class)
fun BookReaderScreen(
    itemId: String,
    onBack: () -> Unit,
    viewModel: BookReaderViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
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
                initialPreferences = ReflowableWebPreferences(),
                initialLocation = state.initialLocator?.let(::ReflowableWebGoLocation),
            )?.getOrNull() ?: error("Não foi possível preparar a leitura.")
        }.onSuccess { renditionState = it }
            .onFailure { renditionError = "Não foi possível renderizar este livro." }
    }
    val renditionController = renditionState?.controller
    LaunchedEffect(itemId, renditionController) {
        val controller = renditionController ?: return@LaunchedEffect
        snapshotFlow { controller.location.toLocator() }
            .distinctUntilChanged()
            .collect { locator -> viewModel.saveReadingProgression(itemId, locator) }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Leitor de livros", maxLines = 1) },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                    }
                },
                actions = {
                    key(itemId) {
                        BookReaderProgressActions(
                            enabled = state.publication != null && !state.isLoading,
                            onRestart = { viewModel.restartReadingFromBeginning(itemId) },
                        )
                    }
                },
            )
        },
        bottomBar = {
            val controller = renditionState?.controller
            Row(
                modifier = Modifier.fillMaxWidth().padding(horizontal = 24.dp, vertical = 8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                IconButton(onClick = { controller?.let { nav -> coroutineScope.launch { nav.moveBackward() } } }, enabled = controller != null) {
                    Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Página anterior")
                }
                Text("Toque nas setas para navegar", style = MaterialTheme.typography.labelMedium)
                IconButton(onClick = { controller?.let { nav -> coroutineScope.launch { nav.moveForward() } } }, enabled = controller != null) {
                    Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = "Próxima página")
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
@OptIn(ExperimentalMaterial3Api::class)
internal fun BookReaderProgressActions(
    enabled: Boolean,
    onRestart: () -> Unit,
) {
    var menuExpanded by remember { mutableStateOf(false) }
    var confirmationVisible by remember { mutableStateOf(false) }

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
                text = { Text("Reiniciar do começo") },
                onClick = {
                    menuExpanded = false
                    confirmationVisible = true
                },
            )
        }
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
private fun ReaderMessage(message: String, onRetry: () -> Unit) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(message, textAlign = TextAlign.Center, modifier = Modifier.padding(24.dp))
        IconButton(onClick = onRetry) {
            Icon(Icons.Default.Refresh, contentDescription = "Tentar carregar o livro novamente")
        }
    }
}
