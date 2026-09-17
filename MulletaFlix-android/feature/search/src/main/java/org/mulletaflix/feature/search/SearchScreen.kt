package org.mulletaflix.feature.search

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
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
                        if (state.query.isNotEmpty()) {
                            IconButton(onClick = { viewModel.onQueryChange("") }) {
                                Icon(Icons.Default.Close, contentDescription = "Limpar")
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
        } else if (state.error != null) {
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
        } else if (state.query.isEmpty()) {
            // Show search history
            SearchHistory(
                history = state.history,
                onItemClick = viewModel::search,
                onRemoveItem = viewModel::removeHistoryItem,
                onClearHistory = viewModel::clearHistory
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
            LazyColumn {
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
                                val cardShape = when (item.type) {
                                    MediaItemType.Movie, MediaItemType.MusicAlbum, MediaItemType.Book -> MediaCardShape.Portrait
                                    else -> MediaCardShape.Landscape
                                }
                                MediaCard(
                                    title = item.name,
                                    imageUrl = item.primaryImageUrl,
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

@Composable
private fun SearchHistory(
    history: List<String>,
    onItemClick: (String) -> Unit,
    onRemoveItem: (String) -> Unit,
    onClearHistory: () -> Unit,
) {
    if (history.isEmpty()) return
    Column {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text("Buscas Recentes", style = MaterialTheme.typography.titleSmall)
            TextButton(onClick = onClearHistory) { Text("Limpar") }
        }
        history.forEach { query ->
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { onItemClick(query) }
                    .padding(horizontal = 16.dp, vertical = 6.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Icon(Icons.Default.History, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
                Text(query, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.weight(1f).padding(horizontal = 12.dp))
                IconButton(onClick = { onRemoveItem(query) }) {
                    Icon(
                        Icons.Default.Close,
                        contentDescription = "Remover da busca",
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(18.dp)
                    )
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
