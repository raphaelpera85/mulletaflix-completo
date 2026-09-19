package org.mulletaflix.feature.library

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.primaryImageUrl

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FavoritesScreen(
    onItemClick: (String) -> Unit,
    onBack: () -> Unit,
    viewModel: FavoritesViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Minha Lista") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                    }
                },
                actions = {
                    IconButton(onClick = viewModel::refresh, enabled = !state.isLoading) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar Minha Lista")
                    }
                },
            )
        },
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding)) {
            when {
                state.isLoading && state.items.isEmpty() ->
                    CircularProgressIndicator(Modifier.align(Alignment.Center))
                state.error != null && state.items.isEmpty() ->
                    ErrorState(state.error!!, viewModel::refresh)
                state.items.isEmpty() ->
                    EmptyFavoritesState()
                else ->
                    FavoritesGrid(state.items, state.hasMore, state.isLoading, onItemClick, viewModel::loadMore)
            }
            if (state.error != null && state.items.isNotEmpty()) {
                Card(
                    Modifier.align(Alignment.BottomCenter).fillMaxWidth().padding(12.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                ) {
                    Text(state.error!!, Modifier.padding(12.dp), color = MaterialTheme.colorScheme.onErrorContainer)
                }
            }
        }
    }
}

@Composable
private fun FavoritesGrid(
    items: List<MediaItem>,
    hasMore: Boolean,
    isLoading: Boolean,
    onItemClick: (String) -> Unit,
    onLoadMore: () -> Unit,
) {
    LazyVerticalGrid(
        columns = GridCells.Fixed(3),
        contentPadding = PaddingValues(8.dp),
        horizontalArrangement = Arrangement.spacedBy(6.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        items(items, key = { it.id }) { item ->
            MediaCard(
                title = item.name,
                imageUrl = item.primaryImageUrl,
                shape = if (item.type == MediaItemType.Movie || item.type == MediaItemType.MusicAlbum || item.type == MediaItemType.Book) MediaCardShape.Portrait else MediaCardShape.Landscape,
                progress = item.playedPercentage?.toFloat()?.div(100f) ?: 0f,
                isWatched = item.isPlayed,
                isFavorite = true,
                unplayedCount = item.unplayedItemCount ?: 0,
                qualityBadge = when { item.has4K -> "4K"; item.hasHD -> "HD"; else -> null },
                onClick = { onItemClick(item.id) },
                modifier = Modifier.fillMaxWidth(),
            )
        }
        if (hasMore) {
            item(span = { GridItemSpan(3) }) {
                LaunchedEffect(items.size) { if (!isLoading) onLoadMore() }
                Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator(Modifier.padding(16.dp).size(28.dp))
                }
            }
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
