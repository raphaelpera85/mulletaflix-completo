package org.mulletaflix.feature.home

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.*
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.platform.LocalConfiguration
import android.content.res.Configuration
import kotlin.math.roundToInt
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavController
import coil.compose.AsyncImage
import coil.compose.SubcomposeAsyncImage
import org.mulletaflix.domain.model.*
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.userAvatarPath
import org.mulletaflix.designsystem.components.MulletaFlixWordmark
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.designsystem.components.isTelevisionDevice
import org.mulletaflix.designsystem.components.downloadsAvailableOnDevice
import org.mulletaflix.designsystem.theme.MulletaFlixRed

/**
 * Home screen — the first screen users see after login.
 *
 * Structure (mirrors MulletaFlix-web homesections):
 *  1. Hero banner (backdrop + title + synopsis + Play/More buttons)
 *  2. Continue Watching — horizontal slider with progress
 *  3. Next Up — next episode in active series
 *  4. Recently Added — by library
 *  5. Live TV — featured channels
 *  6. Active Recordings
 *  7. Library tiles
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HomeScreen(
    onItemClick: (String) -> Unit,
    onPlayItemClick: (String) -> Unit,
    onLibraryClick: (String) -> Unit,
    onLiveTvClick: () -> Unit,
    navController: NavController,
    viewModel: HomeViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var showRequestDialog by remember { mutableStateOf(false) }
    var requestTitle by remember { mutableStateOf("") }
    var requestType by remember { mutableStateOf("Série") }
    var requestYear by remember { mutableStateOf("") }
    var requestNotes by remember { mutableStateOf("") }
    var requestMessage by remember { mutableStateOf<String?>(null) }
    var requestSubmitting by remember { mutableStateOf(false) }
    val requestYearNumber = requestYear.toIntOrNull()
    val requestYearValid = requestYear.isBlank() || (requestYearNumber != null && requestYearNumber in 1888..2200)
    val lifecycleOwner = LocalLifecycleOwner.current
    BoxWithConstraints(modifier = Modifier.fillMaxSize()) {
        val isTelevision = isTelevisionDevice()
        val homeScrollState = rememberHomeScrollState()
        val layoutSpec = homeLayoutSpec(
            homeDeviceClass(maxWidth.value.roundToInt(), isTelevision),
        )

        TvRefreshEffect(
            lifecycleOwner = lifecycleOwner,
            refreshIntervalMillis = homeAutoRefreshIntervalMillis(isTelevision),
            refreshImmediately = refreshHomeImmediatelyOnResume(isTelevision),
            onRefresh = viewModel::refreshIfIdle,
        )

        PullToRefreshBox(
            isRefreshing = state.isRefreshing,
            onRefresh = { viewModel.refresh() },
            modifier = Modifier
                .fillMaxWidth()
                .widthIn(max = layoutSpec.contentMaxWidthDp.dp)
                .align(Alignment.TopCenter),
        ) {
            LazyColumn(
                state = homeScrollState,
                modifier = Modifier
                    .fillMaxSize()
                    .background(MaterialTheme.colorScheme.background)
                    .padding(horizontal = layoutSpec.horizontalPaddingDp.dp),
                verticalArrangement = Arrangement.spacedBy(0.dp),
            ) {
            item {
                HomeTopBar(
                    profile = state.userProfile,
                    layoutSpec = layoutSpec,
                    isTelevision = isTelevision,
                    onSearch = { navController.navigate("main/search") },
                    onLiveTv = { navController.navigate("main/live-tv") },
                    onDownloads = { navController.navigate("main/downloads") },
                    onFavorites = { navController.navigate("main/favorites") },
                    onSettings = { navController.navigate("main/settings") },
                    onProfile = { navController.navigate("main/profile") },
                    onRefresh = { viewModel.refresh() },
                    onRequestMedia = { showRequestDialog = true },
                    isRefreshing = state.isRefreshing,
                )
            }

            if (state.isOffline) {
                item {
                    HomeOfflineStatusCard(
                        cachedAtEpochMillis = state.cachedAtEpochMillis,
                        resumeCached = state.resumeFromCache,
                        favoritesCached = state.favoritesFromCache,
                        downloadsAvailable = downloadsAvailableOnDevice(isTelevision),
                    )
                }
            }

            if (state.isLoading && state.heroItem == null && state.libraries.isEmpty()) {
                item {
                    Box(
                        modifier = Modifier.fillMaxWidth().height(280.dp),
                        contentAlignment = Alignment.Center,
                    ) {
                        CircularProgressIndicator(color = MaterialTheme.colorScheme.secondary)
                    }
                }
            }

            state.error?.let { message ->
                item {
                    HomeLoadErrorCard(
                        title = "Não foi possível carregar o conteúdo",
                        message = message,
                        isTelevision = isTelevision,
                        onRetry = viewModel::refresh,
                    )
                }
            }

            // ── Hero Banner ─────────────────────────────────────────────────
            state.heroItem?.takeUnless { isTelevision && it.type == MediaItemType.Book }?.let { hero ->
                item {
                    HeroBanner(
                        item = hero,
                        heightDp = layoutSpec.heroHeightDp,
                        onPlay = { onPlayItemClick(hero.id) },
                        onMoreInfo = { onItemClick(hero.id) }
                    )
                }
            }

            // ── Continue Watching ────────────────────────────────────────────
            val visibleResumeItems = homeMediaItemsForDevice(state.resumeItems, isTelevision)
            if (visibleResumeItems.isNotEmpty()) {
                item {
                    MediaSection(
                        title = if (state.resumeFromCache) "Continuar Assistindo · salvo" else "Continuar Assistindo",
                        items = visibleResumeItems,
                        cardShape = null,
                        cardWidth = null,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick,
                        onResumeItemClick = onPlayItemClick,
                    )
                }
            } else {
                state.resumeError?.let { message ->
                    item {
                        HomeLoadErrorCard(
                            title = "Não foi possível carregar Continuar Assistindo",
                            message = message,
                            isTelevision = isTelevision,
                            onRetry = viewModel::refresh,
                        )
                    }
                }
            }

            // ── Next Up ──────────────────────────────────────────────────────
            val visibleNextUpItems = homeMediaItemsForDevice(state.nextUpItems, isTelevision)
            if (visibleNextUpItems.isNotEmpty()) {
                item {
                    MediaSection(
                        title = "Próximo Episódio",
                        items = visibleNextUpItems,
                        cardShape = MediaCardShape.Landscape,
                        cardWidth = 240.dp,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick
                    )
                }
            } else {
                state.nextUpError?.let { message ->
                    item {
                        HomeLoadErrorCard(
                            title = "Não foi possível carregar Próximo Episódio",
                            message = message,
                            isTelevision = isTelevision,
                            onRetry = viewModel::refresh,
                        )
                    }
                }
            }

            // ── My List / Favorites ─────────────────────────────────────────
            val visibleFavoriteItems = homeMediaItemsForDevice(state.favoriteItems, isTelevision)
            if (visibleFavoriteItems.isNotEmpty()) {
                item {
                    MediaSection(
                        title = if (state.favoritesFromCache) "Minha Lista · salva" else "Minha Lista",
                        items = visibleFavoriteItems,
                        cardShape = MediaCardShape.Portrait,
                        cardWidth = 130.dp,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick,
                    )
                }
            } else {
                state.favoritesError?.let { message ->
                    item {
                        HomeLoadErrorCard(
                            title = "Não foi possível carregar Minha Lista",
                            message = message,
                            isTelevision = isTelevision,
                            onRetry = viewModel::refresh,
                        )
                    }
                }
            }

            // ── Recently Added (per library) ─────────────────────────────────
            homeRecentLibrarySections(
                libraries = state.libraries,
                recentItemsByLibraryId = state.recentlyAddedByLibrary,
                errorsByLibraryId = state.recentlyAddedErrorsByLibrary,
                isTelevision = isTelevision,
            ).forEach { section ->
                val libraryName = section.library.name
                val items = section.items
                if (items.isNotEmpty()) {
                    item {
                        MediaSection(
                            title = "Adicionados Recentemente — $libraryName",
                            items = items,
                            cardShape = MediaCardShape.Portrait,
                            cardWidth = 130.dp,
                            layoutSpec = layoutSpec,
                            onItemClick = onItemClick,
                        )
                    }
                } else {
                    section.errorMessage?.let { message ->
                        item {
                            HomeLoadErrorCard(
                                title = "Não foi possível carregar Adicionados Recentemente — $libraryName",
                                message = message,
                                isTelevision = isTelevision,
                                isRetrying = section.library.id in state.retryingRecentlyAddedLibraryIds,
                                onRetry = { viewModel.retryRecentlyAdded(section.library.id) },
                            )
                        }
                    }
                }
            }

            // ── Live TV Channels ─────────────────────────────────────────────
            if (state.liveTvChannels.isNotEmpty()) {
                item {
                    MediaSection(
                        title = "TV Ao Vivo",
                        items = state.liveTvChannels,
                        cardShape = MediaCardShape.Landscape,
                        cardWidth = 200.dp,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick,
                        isLive = true
                    )
                }
            }

            // A falha ao buscar os canais some do mesmo jeito que "este servidor não tem
            // TV ao vivo": o carrossel não é desenhado e nada explica a diferença. Como
            // o servidor com TV desligada responde **sucesso com zero canais**, dá para
            // distinguir os dois casos — e o erro fica exatamente onde o carrossel
            // estaria, para não sugerir que o problema é das bibliotecas abaixo.
            state.liveTvError?.let { message ->
                item {
                    HomeLoadErrorCard(
                        title = "Não foi possível carregar a TV ao vivo",
                        message = message,
                        isTelevision = isTelevision,
                        onRetry = viewModel::refresh,
                    )
                }
            }

            // ── Library tiles ────────────────────────────────────────────────
            val visibleLibraries = homeLibrariesForDevice(state.libraries, isTelevision)
            if (visibleLibraries.isNotEmpty()) {
                item {
                    LibraryTiles(
                        libraries = visibleLibraries,
                        layoutSpec = layoutSpec,
                        onLibraryClick = { library ->
                            if (shouldOpenLiveTv(library)) onLiveTvClick() else onLibraryClick(library.id)
                        },
                    )
                }
            }

            // Sem esta linha, uma falha ao listar as bibliotecas desenhava exatamente
            // a Home de quem não tem biblioteca nenhuma: sem bloco, sem erro e sem
            // como tentar de novo. `state.error` continua sendo a falha do feed
            // inteiro — os dois não podem aparecer juntos, porque quando o feed
            // inteiro falha não há `HomeFeed` para carregar `librariesError`.
            state.librariesError?.let { message ->
                item {
                    HomeLoadErrorCard(
                        title = "Não foi possível carregar suas bibliotecas",
                        message = message,
                        isTelevision = isTelevision,
                        onRetry = viewModel::refresh,
                    )
                }
            }

            if (shouldShowEmptyHomeState(state, isTelevision)) {
                item {
                    EmptyHomeState(modifier = Modifier.fillMaxWidth().padding(32.dp))
                }
            }

            // Bottom spacing for nav bar
            item { Spacer(modifier = Modifier.height(80.dp)) }
            }
        }
    }
    if (showRequestDialog) {
        MediaRequestDialog(
            title = requestTitle,
            onTitleChange = { requestTitle = it; viewModel.searchMediaSuggestions(it) },
            suggestions = state.mediaSuggestions,
            onSuggestionSelected = { suggestion ->
                requestTitle = suggestion.title
                requestType = when (suggestion.mediaType.lowercase()) {
                    "movie" -> "Filme"
                    "series" -> "Série"
                    "animation" -> "Animação"
                    "novel" -> "Novela"
                    "dorama" -> "Dorama"
                    else -> "Outro"
                }
                suggestion.year?.let { requestYear = it.toString() }
                viewModel.clearMediaSuggestions()
            },
            mediaType = requestType,
            onMediaTypeChange = { requestType = it },
            year = requestYear,
            onYearChange = { requestYear = it },
            notes = requestNotes,
            onNotesChange = { requestNotes = it },
            message = requestMessage,
            isSubmitting = requestSubmitting,
            hasFeedbackSession = state.hasFeedbackSession,
            feedbackSessionLoaded = state.feedbackSessionLoaded,
            onDismiss = { showRequestDialog = false; requestMessage = null; viewModel.clearMediaSuggestions() },
            onSubmit = {
                requestSubmitting = true
                requestMessage = "Enviando solicitação…"
                viewModel.requestMedia(requestTitle, requestType, requestYear.toIntOrNull(), requestNotes) { result ->
                    requestSubmitting = false
                    result.onSuccess {
                        showRequestDialog = false
                        requestTitle = ""
                        requestYear = ""
                        requestNotes = ""
                        requestMessage = null
                    }.onFailure {
                        requestMessage = if (it is kotlinx.coroutines.CancellationException) {
                            null
                        } else {
                            it.localizedMessage ?: "Não foi possível enviar. Tente novamente."
                        }
                    }
                }
            },
        )
    }

}

@Composable
internal fun MediaRequestDialog(
    title: String,
    onTitleChange: (String) -> Unit,
    suggestions: List<MediaSuggestion> = emptyList(),
    onSuggestionSelected: (MediaSuggestion) -> Unit = {},
    mediaType: String,
    onMediaTypeChange: (String) -> Unit,
    year: String,
    onYearChange: (String) -> Unit,
    notes: String,
    onNotesChange: (String) -> Unit,
    message: String?,
    isSubmitting: Boolean,
    hasFeedbackSession: Boolean,
    feedbackSessionLoaded: Boolean,
    onDismiss: () -> Unit,
    onSubmit: () -> Unit,
) {
    val yearNumber = year.toIntOrNull()
    val yearValid = year.isBlank() || (yearNumber != null && yearNumber in 1888..2200)
    var expanded by remember { mutableStateOf(false) }
    val screenHeightDp = LocalConfiguration.current.screenHeightDp
    val dialogContentMaxHeight = (screenHeightDp - 220).coerceIn(180, 440).dp

    AlertDialog(
        onDismissRequest = { if (!isSubmitting) onDismiss() },
        title = { Text("Solicitar mídia") },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = dialogContentMaxHeight)
                    .verticalScroll(rememberScrollState())
                    .testTag("media-request-content"),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                OutlinedTextField(
                    title,
                    { onTitleChange(it.take(200)) },
                    modifier = Modifier.testTag("media-request-title"),
                    label = { Text("Título") },
                    singleLine = true,
                    enabled = !isSubmitting,
                )
                if (suggestions.isNotEmpty()) {
                    Card {
                        Column {
                            suggestions.forEach { suggestion ->
                                TextButton(
                                    onClick = { onSuggestionSelected(suggestion) },
                                    modifier = Modifier.fillMaxWidth(),
                                ) {
                                    Column(Modifier.fillMaxWidth()) {
                                        Text(suggestion.title, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                        Text(
                                            listOfNotNull(suggestion.mediaType, suggestion.year?.toString()).joinToString(" · "),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
                Box {
                    OutlinedButton(onClick = { expanded = true }, enabled = !isSubmitting) { Text(mediaType) }
                    DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                        listOf("Filme", "Série", "Animação", "Novela", "Dorama", "Livro", "Música", "Outro").forEach { type ->
                            DropdownMenuItem(text = { Text(type) }, onClick = { onMediaTypeChange(type); expanded = false })
                        }
                    }
                }
                OutlinedTextField(
                    year,
                    { onYearChange(it.filter(Char::isDigit).take(4)) },
                    modifier = Modifier.testTag("media-request-year"),
                    label = { Text("Ano (opcional)") },
                    supportingText = { if (!yearValid) Text("Informe um ano entre 1888 e 2200.") },
                    isError = !yearValid,
                    singleLine = true,
                    enabled = !isSubmitting,
                )
                OutlinedTextField(
                    notes,
                    { onNotesChange(it.take(1000)) },
                    modifier = Modifier.testTag("media-request-notes"),
                    label = { Text("Detalhes (opcional)") },
                    minLines = 2,
                    enabled = !isSubmitting,
                )
                if (!hasFeedbackSession) {
                    Text(
                        text = if (feedbackSessionLoaded) "Conecte-se ao servidor para enviar." else "Verificando sessão…",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                message?.let { Text(it, color = if (isSubmitting) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.error) }
            }
        },
        confirmButton = {
            TextButton(enabled = title.isNotBlank() && yearValid && hasFeedbackSession && !isSubmitting, onClick = onSubmit) {
                if (isSubmitting) CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
                else Text("Enviar")
            }
        },
        dismissButton = {
            TextButton(enabled = !isSubmitting, onClick = onDismiss) { Text("Cancelar") }
        },
    )
}

/**
 * O cartão de erro da Home, com o "Tentar novamente" que o torna recuperável.
 *
 * Duas falhas diferentes usam o mesmo desenho: o feed inteiro (`state.error`) e
 * só as bibliotecas (`state.librariesError`). Antes existia um desenho para a
 * primeira e **nenhum** para a segunda, e uma falha ao listar as bibliotecas
 * desenhava a Home de quem não tem biblioteca.
 */
@Composable
internal fun HomeLoadErrorCard(
    title: String,
    message: String,
    isTelevision: Boolean = false,
    isRetrying: Boolean = false,
    onRetry: () -> Unit,
) {
    var isFocused by remember { mutableStateOf(false) }
    Card(
        modifier = Modifier.fillMaxWidth().padding(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.errorContainer,
        ),
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.onErrorContainer,
            )
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onErrorContainer,
                modifier = Modifier.padding(top = 4.dp),
            )
            TextButton(
                enabled = !isRetrying,
                onClick = onRetry,
                modifier = Modifier
                    .padding(top = 4.dp)
                    .onFocusChanged { isFocused = it.isFocused }
                    .then(
                        if (isTelevision && isFocused) {
                            Modifier.border(2.dp, MulletaFlixRed, MaterialTheme.shapes.small)
                        } else {
                            Modifier
                        },
                    ),
            ) {
                if (isRetrying) {
                    CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
                    Spacer(Modifier.width(8.dp))
                    Text("Tentando novamente")
                } else {
                    Text("Tentar novamente")
                }
            }
        }
    }
}

@Composable
private fun EmptyHomeState(modifier: Modifier = Modifier) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(
            imageVector = Icons.Default.MovieFilter,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(52.dp),
        )
        Text(
            text = "Nenhum conteúdo disponível",
            style = MaterialTheme.typography.titleMedium,
            modifier = Modifier.padding(top = 12.dp),
        )
        Text(
            text = "Verifique as bibliotecas configuradas no servidor.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp),
        )
    }
}

// ── Hero Banner ───────────────────────────────────────────────────────────────

@Composable
internal fun HeroBanner(
    item: MediaItem,
    heightDp: Int,
    onPlay: () -> Unit,
    onMoreInfo: () -> Unit,
) {
    val serverUrl = LocalMulletaFlixServerUrl.current
    val accessToken = LocalMulletaFlixAccessToken.current
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(heightDp.dp)
    ) {
        // Blurred backdrop
        AsyncImage(
            model = resolveMediaUrl(serverUrl, item.backdropImageUrl, accessToken),
            contentDescription = null,
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize()
        )

        // Gradient overlay: transparent top → opaque bottom
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(
                    Brush.verticalGradient(
                        colors = listOf(
                            Color.Transparent,
                            Color.Black.copy(alpha = 0.3f),
                            Color.Black.copy(alpha = 0.85f),
                            MaterialTheme.colorScheme.background
                        ),
                        startY = 0f,
                        endY = Float.POSITIVE_INFINITY
                    )
                )
        )

        // Content at bottom
        Column(
            modifier = Modifier
                .align(Alignment.BottomStart)
                .padding(horizontal = 20.dp, vertical = 24.dp)
        ) {
            // Title
            Text(
                text = item.name,
                style = MaterialTheme.typography.headlineMedium,
                color = Color.White,
                fontWeight = FontWeight.Bold,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )

            // Metadata row (year · rating · runtime)
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.padding(top = 4.dp)
            ) {
                item.displayYearRange()?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = Color.White.copy(0.8f)) }
                item.officialRating?.let {
                    Surface(
                        shape = MaterialTheme.shapes.extraSmall,
                        color = Color.White.copy(alpha = 0.2f),
                        modifier = Modifier.padding(horizontal = 4.dp)
                    ) {
                        Text(it, style = MaterialTheme.typography.labelSmall, color = Color.White, modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp))
                    }
                }
                item.runtimeMinutes?.let { Text("${it} min", style = MaterialTheme.typography.bodySmall, color = Color.White.copy(0.8f)) }
            }

            // Overview
            item.overview?.let { overview ->
                Text(
                    text = overview,
                    style = MaterialTheme.typography.bodyMedium,
                    color = Color.White.copy(alpha = 0.85f),
                    maxLines = 3,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.padding(top = 8.dp)
                )
            }

            // Buttons
            Row(
                modifier = Modifier.padding(top = 16.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Button(onClick = onPlay) {
                    Icon(Icons.Default.PlayArrow, contentDescription = null)
                    Spacer(Modifier.width(4.dp))
                    Text("Reproduzir")
                }
                OutlinedButton(onClick = onMoreInfo) {
                    Icon(Icons.Default.Info, contentDescription = null)
                    Spacer(Modifier.width(4.dp))
                    Text("Mais Informações")
                }
            }
        }
    }
}

// ── Media Section (horizontal scroll) ────────────────────────────────────────

@Composable
internal fun MediaSection(
    title: String,
    items: List<MediaItem>,
    cardShape: MediaCardShape?,
    cardWidth: androidx.compose.ui.unit.Dp?,
    layoutSpec: HomeLayoutSpec,
    onItemClick: (String) -> Unit,
    isLive: Boolean = false,
    onResumeItemClick: ((String) -> Unit)? = null,
) {
    val carouselScrollState = rememberHomeCarouselScrollState()
    Column(modifier = Modifier.padding(vertical = 12.dp)) {
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp)
        )
        LazyRow(
            state = carouselScrollState,
            contentPadding = PaddingValues(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(
                items = items,
                key = { item -> item.id },
            ) { item ->
                val resolvedShape = cardShape ?: defaultMediaSectionShape(item)
                val resolvedWidth = (cardWidth ?: if (resolvedShape == MediaCardShape.Portrait) 130.dp else 240.dp) * layoutSpec.cardScale
                Column(modifier = Modifier.width(resolvedWidth)) {
                    MediaCard(
                        title = item.name,
                        imageUrl = item.primaryImageUrl,
                        metadata = item.cardMetadata(),
                        shape = resolvedShape,
                        progress = item.playbackProgressFraction(),
                        isWatched = item.isPlayed,
                        isFavorite = item.isFavorite,
                        unplayedCount = item.unplayedItemCount ?: 0,
                        isLive = isLive,
                        qualityBadge = when {
                            item.has4K -> "4K"
                            item.hasHD -> "HD"
                            else -> null
                        },
                        focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                        onClick = { onItemClick(item.id) },
                        modifier = Modifier.fillMaxWidth(),
                    )
                    if (onResumeItemClick != null && item.hasResumablePlaybackPosition()) {
                        Button(
                            onClick = { onResumeItemClick(item.id) },
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(top = 4.dp)
                                .heightIn(min = 48.dp)
                                .semantics(mergeDescendants = true) {
                                    contentDescription = "Retomar ${item.name}"
                                },
                        ) {
                            Icon(Icons.Default.PlayArrow, contentDescription = null)
                            Spacer(Modifier.width(6.dp))
                            Text("Retomar", maxLines = 1, overflow = TextOverflow.Ellipsis)
                        }
                    }
                }
            }
        }
    }
}

internal fun MediaItem.hasResumablePlaybackPosition(): Boolean {
    if (isPlayed) return false
    val positionTicks = playbackPositionTicks ?: userProgress?.playbackPositionTicks ?: return false
    val durationTicks = runtimeTicks
    return positionTicks > 0L && (durationTicks == null || durationTicks <= 0L || positionTicks < durationTicks)
}

internal fun defaultMediaSectionShape(item: MediaItem): MediaCardShape =
    if (item.type.usesPosterArtwork()) MediaCardShape.Portrait else MediaCardShape.Landscape

// ── Library Tiles ─────────────────────────────────────────────────────────────

@Composable
private fun LibraryTiles(
    libraries: List<MediaItem>,
    layoutSpec: HomeLayoutSpec,
    onLibraryClick: (MediaItem) -> Unit,
) {
    val carouselScrollState = rememberHomeCarouselScrollState()
    Column(modifier = Modifier.padding(vertical = 12.dp)) {
        Text(
            text = "Minhas Bibliotecas",
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp)
        )
        LazyRow(
            state = carouselScrollState,
            contentPadding = PaddingValues(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(
                items = libraries,
                key = { library -> library.id },
            ) { lib ->
                MediaCard(
                    title = lib.name,
                     imageUrl = lib.primaryImageUrl,
                     shape = MediaCardShape.Landscape,
                     focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                     onClick = { onLibraryClick(lib) },
                    modifier = Modifier.width(180.dp * layoutSpec.cardScale)
                )
            }
        }
    }
}

internal fun shouldOpenLiveTv(library: MediaItem): Boolean =
    library.collectionType.equals("livetv", ignoreCase = true)

internal fun homeLibrariesForDevice(libraries: List<MediaItem>, isTelevision: Boolean): List<MediaItem> =
    libraries.filter { shouldShowLibraryOnDevice(it, isTelevision) }

internal fun homeMediaItemsForDevice(items: List<MediaItem>, isTelevision: Boolean): List<MediaItem> =
    if (isTelevision) items.filterNot { it.type == MediaItemType.Book } else items

internal fun shouldShowLibraryOnDevice(library: MediaItem, isTelevision: Boolean): Boolean =
    !isTelevision || !isBooksLibrary(library)

internal data class HomeRecentLibrarySection(
    val library: MediaItem,
    val items: List<MediaItem>,
    val errorMessage: String?,
)

internal fun homeRecentLibrarySections(
    libraries: List<MediaItem>,
    recentItemsByLibraryId: Map<String, List<MediaItem>>,
    errorsByLibraryId: Map<String, String>,
    isTelevision: Boolean,
): List<HomeRecentLibrarySection> = homeLibrariesForDevice(libraries, isTelevision)
    .map { library ->
        HomeRecentLibrarySection(
            library = library,
            items = homeMediaItemsForDevice(recentItemsByLibraryId[library.id].orEmpty(), isTelevision),
            errorMessage = errorsByLibraryId[library.id],
        )
    }
    .filter { section -> section.items.isNotEmpty() || section.errorMessage != null }

private fun isBooksLibrary(library: MediaItem): Boolean =
    library.collectionType?.trim().equals("books", ignoreCase = true) || isBooksLibraryName(library.name)

internal fun isBooksLibraryName(name: String): Boolean =
    name.trim().equals("Livros", ignoreCase = true) || name.trim().equals("Books", ignoreCase = true)

private val MediaItem.runtimeMinutes: Int? get() =
    runtimeTicks?.div(600_000_000L)?.toInt()?.takeIf { it > 0 }

@Composable
internal fun HomeTopBar(
    profile: UserProfile?,
    layoutSpec: HomeLayoutSpec,
    isTelevision: Boolean = false,
    onSearch: () -> Unit,
    onLiveTv: () -> Unit,
    onDownloads: () -> Unit,
    onFavorites: () -> Unit,
    onSettings: () -> Unit,
    onProfile: () -> Unit,
    onRefresh: () -> Unit,
    onRequestMedia: () -> Unit = {},
    isRefreshing: Boolean,
) {
    val serverUrl = LocalMulletaFlixServerUrl.current
    val accessToken = LocalMulletaFlixAccessToken.current
    val avatarUrl = resolveMediaUrl(
        serverUrl,
        userAvatarPath(profile?.id, profile?.primaryImageTag),
        accessToken,
    )
    val profileDescription = profile?.name?.let { "Perfil de $it" } ?: "Meu Perfil"
    var showMoreActions by remember { mutableStateOf(false) }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .statusBarsPadding()
            .padding(
                horizontal = if (layoutSpec.usesCompactTopBar) 8.dp else 16.dp,
                vertical = if (layoutSpec.usesFocusFriendlySpacing) 16.dp else 8.dp,
            ),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
        ) {
        MulletaFlixWordmark(
            modifier = if (layoutSpec.usesCompactTopBar) Modifier.weight(1f, fill = false) else Modifier,
            style = if (layoutSpec.usesCompactTopBar) {
                MaterialTheme.typography.titleSmall
            } else {
                MaterialTheme.typography.titleLarge
            },
            fontWeight = FontWeight.Black,
        )
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(if (layoutSpec.usesCompactTopBar) 0.dp else 4.dp)
        ) {
            HomeTopBarAction(focusFriendly = layoutSpec.usesFocusFriendlySpacing, onClick = onRequestMedia) {
                Icon(Icons.Default.AddCircle, contentDescription = "Solicitar mídia", tint = MaterialTheme.colorScheme.onBackground)
            }
            HomeTopBarAction(
                focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                onClick = onSearch,
            ) {
                Icon(Icons.Default.Search, contentDescription = "Buscar", tint = MaterialTheme.colorScheme.onBackground)
            }
            if (!layoutSpec.usesCompactTopBar) {
                HomeTopBarAction(
                    focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                    onClick = onLiveTv,
                ) {
                    Icon(Icons.Default.Tv, contentDescription = "TV Ao Vivo", tint = MaterialTheme.colorScheme.onBackground)
                }
                if (downloadsAvailableOnDevice(isTelevision)) {
                    HomeTopBarAction(
                        focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                        onClick = onDownloads,
                    ) {
                        Icon(Icons.Default.FileDownload, contentDescription = "Downloads", tint = MaterialTheme.colorScheme.onBackground)
                    }
                }
            }
            HomeTopBarAction(
                focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                onClick = onFavorites,
            ) {
                Icon(Icons.Default.Favorite, contentDescription = "Minha Lista", tint = MaterialTheme.colorScheme.secondary)
            }
            if (layoutSpec.usesCompactTopBar) {
                Box {
                    HomeTopBarAction(
                        focusFriendly = false,
                        onClick = { showMoreActions = true },
                    ) {
                        Icon(Icons.Default.MoreVert, contentDescription = "Mais ações", tint = MaterialTheme.colorScheme.onBackground)
                    }
                    DropdownMenu(expanded = showMoreActions, onDismissRequest = { showMoreActions = false }) {
                        DropdownMenuItem(text = { Text("TV Ao Vivo") }, onClick = { showMoreActions = false; onLiveTv() })
                        if (downloadsAvailableOnDevice(isTelevision)) {
                            DropdownMenuItem(text = { Text("Downloads") }, onClick = { showMoreActions = false; onDownloads() })
                        }
                        DropdownMenuItem(text = { Text("Configurações") }, onClick = { showMoreActions = false; onSettings() })
                        DropdownMenuItem(text = { Text("Atualizar Home") }, onClick = { showMoreActions = false; onRefresh() })
                        DropdownMenuItem(text = { Text(profileDescription) }, onClick = { showMoreActions = false; onProfile() })
                    }
                }
            } else {
                HomeTopBarAction(
                    focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                    onClick = onSettings,
                ) {
                    Icon(Icons.Default.Settings, contentDescription = "Configurações", tint = MaterialTheme.colorScheme.onBackground)
                }
                HomeTopBarAction(
                    focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                    onClick = onRefresh,
                    busy = isRefreshing,
                    busyContentDescription = "Atualizando Home",
                ) {
                    Icon(Icons.Default.Refresh, contentDescription = "Atualizar Home", tint = MaterialTheme.colorScheme.onBackground)
                }
                HomeTopBarAction(
                    focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                    onClick = onProfile,
                ) {
                    if (avatarUrl == null) {
                        Icon(Icons.Default.AccountCircle, contentDescription = profileDescription, tint = MaterialTheme.colorScheme.secondary)
                    } else {
                        SubcomposeAsyncImage(
                            model = avatarUrl,
                            contentDescription = profileDescription,
                            contentScale = ContentScale.Crop,
                            modifier = Modifier.size(32.dp).clip(CircleShape),
                            loading = { Icon(Icons.Default.AccountCircle, contentDescription = null, tint = MaterialTheme.colorScheme.secondary) },
                            error = { Icon(Icons.Default.AccountCircle, contentDescription = null, tint = MaterialTheme.colorScheme.secondary) },
                        )
                    }
                }
            }
        }
    }
}

/**
 * One top-bar action. The focus treatment lives in `:design-system`, shared with
 * every other screen's top bar — this wrapper only binds the Home layout's
 * TV spacing to it.
 */
@Composable
private fun HomeTopBarAction(
    focusFriendly: Boolean,
    onClick: () -> Unit,
    busy: Boolean = false,
    busyContentDescription: String? = null,
    content: @Composable () -> Unit,
) {
    MulletaFlixTopBarAction(
        onClick = onClick,
        focusFriendly = focusFriendly,
        busy = busy,
        busyContentDescription = busyContentDescription,
        content = content,
    )
}
