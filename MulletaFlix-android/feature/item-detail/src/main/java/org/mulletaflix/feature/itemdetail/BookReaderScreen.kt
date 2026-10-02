@file:OptIn(org.readium.r2.shared.ExperimentalReadiumApi::class)

package org.mulletaflix.feature.itemdetail

import android.app.Application
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.launch
import org.readium.navigator.web.fixedlayout.FixedWebRendition
import org.readium.navigator.web.fixedlayout.FixedWebConfiguration
import org.readium.navigator.web.fixedlayout.FixedWebGoLocation
import org.readium.navigator.web.fixedlayout.FixedWebRenditionFactory
import org.readium.navigator.web.fixedlayout.FixedWebRenditionState
import org.readium.navigator.web.fixedlayout.preferences.FixedWebPreferences
import org.readium.navigator.web.reflowable.ReflowableWebRendition
import org.readium.navigator.web.reflowable.ReflowableWebConfiguration
import org.readium.navigator.web.reflowable.ReflowableWebGoLocation
import org.readium.navigator.web.reflowable.ReflowableWebRenditionFactory
import org.readium.navigator.web.reflowable.ReflowableWebRenditionState
import org.readium.r2.shared.publication.Layout
import org.readium.r2.shared.publication.Locator
import org.readium.r2.shared.publication.Publication

private sealed interface ReaderRendition {
    data object Loading : ReaderRendition
    data class Reflowable(val state: ReflowableWebRenditionState) : ReaderRendition
    data class Fixed(val state: FixedWebRenditionState) : ReaderRendition
    data class Error(val message: String) : ReaderRendition
}

@Composable
fun BookReaderScreen(
    itemId: String,
    onBack: () -> Unit,
) {
    val viewModel: BookReaderViewModel = hiltViewModel()
    val state by viewModel.state.collectAsStateWithLifecycle()

    LaunchedEffect(itemId) {
        viewModel.load(itemId)
    }

    BookReaderContent(
        state = state,
        onBack = onBack,
        onRetry = { viewModel.retry(itemId) },
        onLocationChanged = viewModel::updateLocation,
    )
}

@Composable
internal fun BookReaderContent(
    state: BookReaderUiState,
    onBack: () -> Unit,
    onRetry: () -> Unit,
    onLocationChanged: (Locator) -> Unit = {},
) {
    Surface(modifier = Modifier.fillMaxSize()) {
        when {
            state.isLoading -> ReaderMessage(
                title = "Abrindo livro…",
                showProgress = true,
                onBack = onBack,
            )

            state.errorMessage != null -> ReaderMessage(
                title = "Não foi possível abrir o livro",
                message = state.errorMessage,
                onBack = onBack,
                onRetry = onRetry,
            )

            state.publication != null -> BookPublicationReader(
                publication = state.publication,
                initialLocation = state.lastLocation,
                onBack = onBack,
                onLocationChanged = onLocationChanged,
            )

            else -> ReaderMessage(
                title = "Não foi possível abrir o livro",
                onBack = onBack,
                onRetry = onRetry,
            )
        }
    }
}

@Composable
private fun BookPublicationReader(
    publication: Publication,
    initialLocation: Locator?,
    onBack: () -> Unit,
    onLocationChanged: (Locator) -> Unit,
) {
    val context = LocalContext.current
    val application = context.applicationContext as Application
    val scope = rememberCoroutineScope()
    var rendition by remember(publication) { mutableStateOf<ReaderRendition>(ReaderRendition.Loading) }

    LaunchedEffect(publication) {
        rendition = ReaderRendition.Loading
        rendition = try {
            if (publication.metadata.layout == Layout.FIXED) {
                val factory = FixedWebRenditionFactory(
                    application = application,
                    publication = publication,
                    configuration = FixedWebConfiguration(),
                ) ?: return@LaunchedEffect run {
                    rendition = ReaderRendition.Error("Este EPUB não é compatível com o leitor de layout fixo.")
                }
                val state = factory.createRenditionState(
                    initialPreferences = FixedWebPreferences(),
                    initialLocation = initialLocation?.let(::FixedWebGoLocation),
                ).getOrNull()

                if (state == null) {
                    ReaderRendition.Error("Não foi possível preparar este EPUB de layout fixo.")
                } else {
                    ReaderRendition.Fixed(state)
                }
            } else {
                val factory = ReflowableWebRenditionFactory(
                    application = application,
                    publication = publication,
                    configuration = ReflowableWebConfiguration(),
                ) ?: return@LaunchedEffect run {
                    rendition = ReaderRendition.Error("Este EPUB não é compatível com o leitor refluível.")
                }
                val state = factory.createRenditionState(
                    initialLocation = initialLocation?.let(::ReflowableWebGoLocation),
                ).getOrNull()

                if (state == null) {
                    ReaderRendition.Error("Não foi possível preparar este EPUB.")
                } else {
                    ReaderRendition.Reflowable(state)
                }
            }
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (error: Throwable) {
            ReaderRendition.Error(error.message ?: "Não foi possível preparar o leitor.")
        }
    }

    val currentLocation = when (val current = rendition) {
        is ReaderRendition.Reflowable -> current.state.controller?.location?.toLocator()
        is ReaderRendition.Fixed -> current.state.controller?.location?.toLocator()
        else -> null
    }
    LaunchedEffect(currentLocation) {
        currentLocation?.let(onLocationChanged)
    }

    Column(modifier = Modifier.fillMaxSize().testTag("book-reader-screen")) {
        ReaderToolbar(
            title = publication.metadata.title ?: "Livro",
            onBack = onBack,
        )

        Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
            when (val current = rendition) {
                ReaderRendition.Loading -> CircularProgressIndicator(Modifier.align(Alignment.Center))
                is ReaderRendition.Error -> Text(
                    text = current.message,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                )
                is ReaderRendition.Reflowable -> ReflowableWebRendition(
                    state = current.state,
                    modifier = Modifier.fillMaxSize(),
                )
                is ReaderRendition.Fixed -> FixedWebRendition(
                    state = current.state,
                    modifier = Modifier.fillMaxSize(),
                )
            }
        }

        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Button(
                onClick = {
                    scope.launch {
                        when (val current = rendition) {
                            is ReaderRendition.Reflowable -> current.state.controller?.let { controller ->
                                controller.moveBackward()
                            }
                            is ReaderRendition.Fixed -> current.state.controller?.let { controller ->
                                controller.moveBackward()
                            }
                            else -> Unit
                        }
                    }
                },
                enabled = when (val current = rendition) {
                    is ReaderRendition.Reflowable -> current.state.controller?.canMoveBackward == true
                    is ReaderRendition.Fixed -> current.state.controller?.canMoveBackward == true
                    else -> false
                },
                modifier = Modifier.testTag("book-reader-previous"),
            ) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = null)
                Text("Anterior")
            }

            Button(
                onClick = {
                    scope.launch {
                        when (val current = rendition) {
                            is ReaderRendition.Reflowable -> current.state.controller?.let { controller ->
                                controller.moveForward()
                            }
                            is ReaderRendition.Fixed -> current.state.controller?.let { controller ->
                                controller.moveForward()
                            }
                            else -> Unit
                        }
                    }
                },
                enabled = when (val current = rendition) {
                    is ReaderRendition.Reflowable -> current.state.controller?.canMoveForward == true
                    is ReaderRendition.Fixed -> current.state.controller?.canMoveForward == true
                    else -> false
                },
                modifier = Modifier.testTag("book-reader-next"),
            ) {
                Text("Próximo")
                Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = null)
            }
        }
    }
}

@Composable
private fun ReaderToolbar(
    title: String,
    onBack: () -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconButton(onClick = onBack) {
            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
        }
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f).padding(horizontal = 8.dp),
        )
    }
}

@Composable
private fun ReaderMessage(
    title: String,
    message: String? = null,
    showProgress: Boolean = false,
    onBack: () -> Unit,
    onRetry: (() -> Unit)? = null,
) {
    Column(modifier = Modifier.fillMaxSize()) {
        ReaderToolbar(title = "Leitor de livro", onBack = onBack)
        Column(
            modifier = Modifier.weight(1f).fillMaxWidth().padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            if (showProgress) {
                CircularProgressIndicator()
                Spacer(Modifier.height(16.dp))
            }
            Text(title, style = MaterialTheme.typography.titleMedium)
            message?.let {
                Spacer(Modifier.height(8.dp))
                Text(it, style = MaterialTheme.typography.bodyMedium)
            }
            onRetry?.let { retry ->
                Spacer(Modifier.height(16.dp))
                Button(onClick = retry) { Text("Tentar novamente") }
            }
        }
    }
}
