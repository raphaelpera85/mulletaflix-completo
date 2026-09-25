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
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
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
    onLibraryClick: (String) -> Unit,
    onLiveTvClick: () -> Unit,
    navController: NavController,
    viewModel: HomeViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
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
                    onSearch = { navController.navigate("main/search") },
                    onLiveTv = { navController.navigate("main/live-tv") },
                    onDownloads = { navController.navigate("main/downloads") },
                    onFavorites = { navController.navigate("main/favorites") },
                    onSettings = { navController.navigate("main/settings") },
                    onProfile = { navController.navigate("main/profile") },
                    onRefresh = { viewModel.refresh() },
                    isRefreshing = state.isRefreshing,
                )
            }

            if (state.isOffline) {
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                    ) {
                        Row(
                            modifier = Modifier.padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Icon(
                                imageVector = Icons.Default.CloudOff,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Text(
                                text = "Você está offline. Acesse Downloads para reproduzir mídias baixadas.",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }
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
            state.heroItem?.let { hero ->
                item {
                    HeroBanner(
                        item = hero,
                        heightDp = layoutSpec.heroHeightDp,
                        onPlay = { onItemClick(hero.id) },
                        onMoreInfo = { onItemClick(hero.id) }
                    )
                }
            }

            // ── Continue Watching ────────────────────────────────────────────
            if (state.resumeItems.isNotEmpty()) {
                item {
                    MediaSection(
                        title = "Continuar Assistindo",
                        items = state.resumeItems,
                        cardShape = null,
                        cardWidth = null,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick
                    )
                }
            }

            // ── Next Up ──────────────────────────────────────────────────────
            if (state.nextUpItems.isNotEmpty()) {
                item {
                    MediaSection(
                        title = "Próximo Episódio",
                        items = state.nextUpItems,
                        cardShape = MediaCardShape.Landscape,
                        cardWidth = 240.dp,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick
                    )
                }
            }

            // ── My List / Favorites ─────────────────────────────────────────
            if (state.favoriteItems.isNotEmpty()) {
                item {
                    MediaSection(
                        title = "Minha Lista",
                        items = state.favoriteItems,
                        cardShape = MediaCardShape.Portrait,
                        cardWidth = 130.dp,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick,
                    )
                }
            }

            // ── Recently Added (per library) ─────────────────────────────────
            state.recentlyAddedByLibrary.forEach { (libraryName, items) ->
                item {
                    MediaSection(
                        title = "Adicionados Recentemente — $libraryName",
                        items = items,
                        cardShape = MediaCardShape.Portrait,
                        cardWidth = 130.dp,
                        layoutSpec = layoutSpec,
                        onItemClick = onItemClick
                    )
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
            if (state.libraries.isNotEmpty()) {
                item {
                    LibraryTiles(
                        libraries = state.libraries,
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

            if (!state.isLoading && state.error == null && state.heroItem == null &&
                state.resumeItems.isEmpty() && state.libraries.isEmpty()
            ) {
                item {
                    EmptyHomeState(modifier = Modifier.fillMaxWidth().padding(32.dp))
                }
            }

            // Bottom spacing for nav bar
            item { Spacer(modifier = Modifier.height(80.dp)) }
            }
        }
    }

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
                Text("Tentar novamente")
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
private fun HeroBanner(
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
private fun MediaSection(
    title: String,
    items: List<MediaItem>,
    cardShape: MediaCardShape?,
    cardWidth: androidx.compose.ui.unit.Dp?,
    layoutSpec: HomeLayoutSpec,
    onItemClick: (String) -> Unit,
    isLive: Boolean = false,
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
                    modifier = Modifier.width(resolvedWidth)
                )
            }
        }
    }
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

private val MediaItem.runtimeMinutes: Int? get() =
    runtimeTicks?.div(600_000_000L)?.toInt()?.takeIf { it > 0 }

@Composable
internal fun HomeTopBar(
    profile: UserProfile?,
    layoutSpec: HomeLayoutSpec,
    onSearch: () -> Unit,
    onLiveTv: () -> Unit,
    onDownloads: () -> Unit,
    onFavorites: () -> Unit,
    onSettings: () -> Unit,
    onProfile: () -> Unit,
    onRefresh: () -> Unit,
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

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .statusBarsPadding()
            .padding(horizontal = 16.dp, vertical = if (layoutSpec.usesFocusFriendlySpacing) 16.dp else 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        MulletaFlixWordmark(
            style = MaterialTheme.typography.titleLarge,
            fontWeight = FontWeight.Black,
        )
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            HomeTopBarAction(
                focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                onClick = onSearch,
            ) {
                Icon(Icons.Default.Search, contentDescription = "Buscar", tint = MaterialTheme.colorScheme.onBackground)
            }
            HomeTopBarAction(
                focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                onClick = onLiveTv,
            ) {
                Icon(Icons.Default.Tv, contentDescription = "TV Ao Vivo", tint = MaterialTheme.colorScheme.onBackground)
            }
            HomeTopBarAction(
                focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                onClick = onDownloads,
            ) {
                Icon(Icons.Default.FileDownload, contentDescription = "Downloads", tint = MaterialTheme.colorScheme.onBackground)
            }
            HomeTopBarAction(
                focusFriendly = layoutSpec.usesFocusFriendlySpacing,
                onClick = onFavorites,
            ) {
                Icon(Icons.Default.Favorite, contentDescription = "Minha Lista", tint = MaterialTheme.colorScheme.secondary)
            }
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
                    Icon(
                        Icons.Default.AccountCircle,
                        contentDescription = profileDescription,
                        tint = MaterialTheme.colorScheme.secondary,
                    )
                } else {
                    SubcomposeAsyncImage(
                        model = avatarUrl,
                        contentDescription = profileDescription,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.size(32.dp).clip(CircleShape),
                        loading = {
                            Icon(Icons.Default.AccountCircle, contentDescription = null, tint = MaterialTheme.colorScheme.secondary)
                        },
                        error = {
                            Icon(Icons.Default.AccountCircle, contentDescription = null, tint = MaterialTheme.colorScheme.secondary)
                        },
                    )
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
