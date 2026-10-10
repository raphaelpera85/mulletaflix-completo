package org.mulletaflix.feature.library

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.zIndex
import android.content.res.Configuration
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.cardMetadata
import org.mulletaflix.domain.model.playbackProgressFraction
import org.mulletaflix.domain.model.primaryImageUrl
import org.mulletaflix.domain.model.usesPosterArtwork

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FavoritesScreen(
    onItemClick: (String) -> Unit,
    onBack: () -> Unit,
    viewModel: FavoritesViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val lifecycleOwner = LocalLifecycleOwner.current
    val configuration = LocalConfiguration.current
    val isTelevision = (configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
        Configuration.UI_MODE_TYPE_TELEVISION
    val visibleItems = favoritesItemsForDevice(state.items, isTelevision)
    val gridColumns = favoritesGridColumns(
        widthDp = configuration.screenWidthDp,
        isTelevision = isTelevision,
        density = state.gridDensity,
    )
    val gridState = rememberLibraryGridScrollState()

    LaunchedEffect(isTelevision, visibleItems.size, state.items.size, state.hasMore, state.isLoading) {
        if (isTelevision && visibleItems.isEmpty() && state.hasMore && !state.isLoading) {
            viewModel.loadMore()
        }
    }

    TvRefreshEffect(
        lifecycleOwner = lifecycleOwner,
        refreshIntervalMillis = favoritesAutoRefreshIntervalMillis(isTelevision),
        refreshImmediately = isTelevision,
        onRefresh = viewModel::refreshIfIdle,
        onResumeRefresh = viewModel::refreshOnResume,
    )

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Minha Lista") },
                navigationIcon = {
                    MulletaFlixTopBarAction(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                    }
                },
                actions = {
                    MulletaFlixTopBarAction(
                        onClick = viewModel::refresh,
                        busy = state.isLoading,
                        busyContentDescription = "Atualizar Minha Lista",
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar Minha Lista")
                    }
                },
            )
        },
    ) { padding ->
        PullToRefreshBox(
            isRefreshing = state.isRefreshing,
            onRefresh = viewModel::refresh,
            modifier = Modifier.fillMaxSize().padding(padding),
        ) {
            Column(Modifier.fillMaxSize()) {
                FavoritesCatalogControls(
                    query = state.searchQuery,
                    sortBy = state.sortBy,
                    sortOrder = state.sortOrder,
                    showSortMenu = state.showSortMenu,
                    isOffline = state.isOffline,
                    onQueryChange = viewModel::setSearchQuery,
                    onToggleSortMenu = viewModel::toggleSortMenu,
                    onDismissSortMenu = viewModel::dismissSortMenu,
                    onApplySort = viewModel::setSort,
                )
                Box(Modifier.fillMaxWidth().weight(1f)) {
                    when {
                        state.isLoading && visibleItems.isEmpty() ->
                            CircularProgressIndicator(Modifier.align(Alignment.Center))
                        state.error != null && visibleItems.isEmpty() ->
                            ErrorState(state.error!!, viewModel::refresh)
                        visibleItems.isEmpty() && state.searchQuery.isNotBlank() ->
                            EmptyFavoritesSearchState { viewModel.setSearchQuery("") }
                        visibleItems.isEmpty() ->
                            EmptyFavoritesState()
                        else ->
                            FavoritesGrid(visibleItems, state.hasMore, state.isLoading, gridColumns, isTelevision, gridState, onItemClick, viewModel::loadMore)
                    }
                    if (state.error != null && visibleItems.isNotEmpty()) {
                        FavoritesInlineError(
                            message = state.error!!,
                            onRetry = viewModel::refresh,
                            modifier = Modifier.align(Alignment.BottomCenter).fillMaxWidth().padding(12.dp),
                        )
                    }
                    if (state.isOffline) {
                        LibraryOfflineBanner(
                            message = "Sem conexão. Minha Lista será atualizada quando a rede voltar.",
                            onRetry = viewModel::refresh,
                            modifier = Modifier
                                .align(Alignment.TopCenter)
                                .padding(12.dp)
                                .zIndex(1f),
                        )
                    }
                }
            }
        }
    }
}

@Composable
internal fun FavoritesCatalogControls(
    query: String,
    sortBy: SortOption,
    sortOrder: SortOrder,
    showSortMenu: Boolean,
    isOffline: Boolean,
    onQueryChange: (String) -> Unit,
    onToggleSortMenu: () -> Unit,
    onDismissSortMenu: () -> Unit,
    onApplySort: (SortOption, SortOrder) -> Unit,
) {
    Column(
        modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        OutlinedTextField(
            value = query,
            onValueChange = onQueryChange,
            enabled = !isOffline,
            modifier = Modifier.fillMaxWidth().semantics {
                contentDescription = "Buscar títulos em Minha Lista"
            },
            label = { Text("Buscar títulos") },
            leadingIcon = { Icon(Icons.Default.Search, contentDescription = null) },
            trailingIcon = if (query.isNotEmpty()) {
                {
                    IconButton(
                        onClick = { onQueryChange("") },
                        modifier = Modifier.semantics { contentDescription = "Limpar busca" },
                    ) { Icon(Icons.Default.Clear, contentDescription = null) }
                }
            } else {
                null
            },
            singleLine = true,
        )
        Box(Modifier.fillMaxWidth()) {
            Button(
                onClick = onToggleSortMenu,
                enabled = !isOffline,
                modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp),
            ) {
                Text("Ordenar: ${sortBy.label} · ${sortOrder.label}")
            }
            if (showSortMenu) {
                SortDropdown(
                    current = sortBy,
                    currentOrder = sortOrder,
                    onApply = onApplySort,
                    onDismiss = onDismissSortMenu,
                    options = FAVORITES_SORT_OPTIONS,
                )
            }
        }
    }
}

private val FAVORITES_SORT_OPTIONS = SortOption.values().filter { it != SortOption.Random }

@Composable
internal fun FavoritesInlineError(
    message: String,
    onRetry: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Card(
        modifier = modifier,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
    ) {
        Row(
            modifier = Modifier.padding(start = 12.dp, end = 4.dp, top = 4.dp, bottom = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                message,
                Modifier.weight(1f),
                color = MaterialTheme.colorScheme.onErrorContainer,
            )
            TextButton(onClick = onRetry) { Text("Tentar novamente") }
        }
    }
}

@Composable
private fun FavoritesGrid(
    items: List<MediaItem>,
    hasMore: Boolean,
    isLoading: Boolean,
    gridColumns: Int,
    isTelevision: Boolean,
    gridState: androidx.compose.foundation.lazy.grid.LazyGridState,
    onItemClick: (String) -> Unit,
    onLoadMore: () -> Unit,
) {
    LazyVerticalGrid(
        state = gridState,
        columns = GridCells.Fixed(gridColumns),
        contentPadding = PaddingValues(8.dp),
        horizontalArrangement = Arrangement.spacedBy(6.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        items(items, key = { it.id }) { item ->
            MediaCard(
                title = item.name,
                imageUrl = item.primaryImageUrl,
                metadata = item.cardMetadata(),
                shape = if (item.type.usesPosterArtwork()) MediaCardShape.Portrait else MediaCardShape.Landscape,
                progress = item.playbackProgressFraction(),
                isWatched = item.isPlayed,
                isFavorite = true,
                unplayedCount = item.unplayedItemCount ?: 0,
                qualityBadge = when { item.has4K -> "4K"; item.hasHD -> "HD"; else -> null },
                focusFriendly = isTelevision,
                onClick = { onItemClick(item.id) },
                modifier = Modifier.fillMaxWidth(),
            )
        }
        if (hasMore) {
            item(span = { GridItemSpan(gridColumns) }) {
                LaunchedEffect(items.size) { if (!isLoading) onLoadMore() }
                Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator(Modifier.padding(16.dp).size(28.dp))
                }
            }
        }
    }
}

@Composable
private fun EmptyFavoritesSearchState(onClearSearch: () -> Unit) {
    Box(Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Icon(Icons.Default.Search, contentDescription = null, modifier = Modifier.size(56.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant)
            Text("Nenhum título encontrado", style = MaterialTheme.typography.titleMedium)
            Text("Tente outro nome ou limpe a busca.", color = MaterialTheme.colorScheme.onSurfaceVariant)
            TextButton(onClick = onClearSearch) { Text("Limpar busca") }
        }
    }
}

@Composable
private fun EmptyFavoritesState() {
    Box(Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
        androidx.compose.foundation.layout.Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Icon(Icons.Default.Favorite, contentDescription = null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.secondary)
            Text("Sua lista está vazia", style = MaterialTheme.typography.titleMedium)
            Text("Toque no coração de um título para encontrá-lo aqui.", color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun ErrorState(message: String, onRetry: () -> Unit) {
    androidx.compose.foundation.layout.Column(
        Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(12.dp, Alignment.CenterVertically),
    ) {
        Text(message, color = MaterialTheme.colorScheme.error)
        Button(onClick = onRetry) { Text("Tentar novamente") }
    }
}
