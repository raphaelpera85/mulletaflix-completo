package org.mulletaflix.feature.library

import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.Sort
import androidx.compose.material.icons.automirrored.filled.ViewList
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
 * Library browser screen.
 *
 * Features:
 *  - Grid/List toggle view
 *  - Sort by: Name, Date Added, Release Date, Runtime, Rating, Random
 *  - Filter chips: Genres, Year, Rating, Resolution (4K/HD), Played/Unplayed, Favorites
 *  - Alpha index picker (A-Z fast scroll)
 *  - Pull to refresh
 *  - Infinite scroll (pagination)
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LibraryScreen(
    libraryId: String,
    onItemClick: (String) -> Unit,
    onBack: () -> Unit,
    viewModel: LibraryViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val loadError = state.error

    LaunchedEffect(libraryId) { viewModel.loadLibrary(libraryId) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(state.libraryName) },
                navigationIcon = {
                    IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar") }
                },
                actions = {
                    IconButton(onClick = { viewModel.loadLibrary(libraryId) }, enabled = !state.isLoading) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar biblioteca")
                    }
                    // View toggle (grid / list)
                    IconButton(onClick = viewModel::toggleView) {
                        Icon(if (state.isGridView) Icons.AutoMirrored.Filled.ViewList else Icons.Default.GridView, contentDescription = "Alternar visualização")
                    }
                    // Sort
                    IconButton(onClick = viewModel::showSortMenu) {
                        Icon(Icons.AutoMirrored.Filled.Sort, contentDescription = "Ordenar")
                    }
                    // Filter
                    IconButton(onClick = viewModel::showFilterMenu) {
                        Icon(Icons.Default.FilterList, contentDescription = "Filtrar")
                    }
                }
            )
        }
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            if (state.isLoading && state.items.isEmpty()) {
                CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
            } else if (loadError != null && state.items.isEmpty()) {
                Column(
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Text(loadError, color = MaterialTheme.colorScheme.error)
                    Button(onClick = { viewModel.loadLibrary(libraryId) }) {
                        Text("Tentar novamente")
                    }
                }
            } else if (state.items.isEmpty()) {
                EmptyLibraryState(
                    hasFilters = state.activeFilters.isNotEmpty(),
                    onClearFilters = viewModel::clearFilters,
                )
            } else {
                val columns = if (state.isGridView) 3 else 1

                LazyVerticalGrid(
                    columns = GridCells.Fixed(columns),
                    contentPadding = PaddingValues(8.dp),
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    // Active filters summary
                    if (loadError != null) {
                        item(span = { GridItemSpan(columns) }) {
                            Card(
                                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                                modifier = Modifier.fillMaxWidth(),
                            ) {
                                Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
                                    Text(loadError, Modifier.weight(1f), color = MaterialTheme.colorScheme.onErrorContainer)
                                    TextButton(onClick = { viewModel.loadLibrary(libraryId) }) { Text("Tentar novamente") }
                                }
                            }
                        }
                    }
                    if (state.activeFilters.isNotEmpty()) {
                        item(span = { GridItemSpan(columns) }) {
                            ActiveFiltersRow(
                                filters = state.activeFilters,
                                onRemoveFilter = viewModel::removeFilter,
                                onClearAll = viewModel::clearFilters
                            )
                        }
                    }

                    items(state.items) { item ->
                        if (state.isGridView) {
                            MediaCard(
                                title = item.name,
                                imageUrl = item.primaryImageUrl,
                                shape = if (item.type == MediaItemType.Movie || item.type == MediaItemType.MusicAlbum || item.type == MediaItemType.Book) MediaCardShape.Portrait else MediaCardShape.Landscape,
                                progress = item.playedPercentage?.toFloat()?.div(100f) ?: 0f,
                                isWatched = item.isPlayed,
                                unplayedCount = item.unplayedItemCount ?: 0,
                                qualityBadge = when { item.has4K -> "4K"; item.hasHD -> "HD"; else -> null },
                                onClick = { onItemClick(item.id) },
                                modifier = Modifier.fillMaxWidth()
                            )
                        } else {
                            LibraryListRow(item = item, onClick = { onItemClick(item.id) })
                        }
                    }

                    // Load more trigger
                    if (state.hasMore) {
                        item(span = { GridItemSpan(columns) }) {
                            LaunchedEffect(Unit) { viewModel.loadMore() }
                            Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                                CircularProgressIndicator(modifier = Modifier.padding(16.dp))
                            }
                        }
                    }

                    item(span = { GridItemSpan(columns) }) {
                        Spacer(modifier = Modifier.height(80.dp))
                    }
                }
            }

            // Sort dropdown
            if (state.showSortMenu) {
                SortDropdown(
                    current = state.sortBy,
                    onSelect = viewModel::setSortBy,
                    onDismiss = viewModel::hideSortMenu
                )
            }
            if (state.showFilterMenu) {
                FilterDialog(
                    activeFilters = state.activeFilters,
                    onToggle = viewModel::toggleFilter,
                    onClear = viewModel::clearFilters,
                    onDismiss = viewModel::hideFilterMenu,
                )
            }
        }
    }
}

@Composable
private fun FilterDialog(
    activeFilters: List<String>,
    onToggle: (String) -> Unit,
    onClear: () -> Unit,
    onDismiss: () -> Unit,
) {
    val filters = listOf(
        LibraryViewModel.FILTER_FAVORITES,
        LibraryViewModel.FILTER_PLAYED,
        LibraryViewModel.FILTER_UNPLAYED,
    )
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Filtrar biblioteca") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                filters.forEach { filter ->
                    FilterChip(
                        selected = filter in activeFilters,
                        onClick = { onToggle(filter) },
                        label = { Text(filter) },
                        leadingIcon = if (filter in activeFilters) {
                            { Icon(Icons.Default.Check, contentDescription = null) }
                        } else null,
                    )
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("Fechar") }
        },
        dismissButton = if (activeFilters.isNotEmpty()) {
            { TextButton(onClick = onClear) { Text("Limpar") } }
        } else null,
    )
}

@Composable
private fun LibraryListRow(item: MediaItem, onClick: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick).padding(4.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        MediaCard(
            title = item.name,
            imageUrl = item.primaryImageUrl,
            shape = MediaCardShape.Portrait,
            isWatched = item.isPlayed,
            unplayedCount = item.unplayedItemCount ?: 0,
            onClick = onClick,
            modifier = Modifier.width(60.dp)
        )
        Column(modifier = Modifier.weight(1f).padding(horizontal = 12.dp)) {
            Text(item.name, style = MaterialTheme.typography.titleSmall, maxLines = 2)
            item.displayYearRange()?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            item.overview?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis) }
        }
        Icon(Icons.Default.ChevronRight, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun ActiveFiltersRow(filters: List<String>, onRemoveFilter: (String) -> Unit, onClearAll: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 4.dp),
        horizontalArrangement = Arrangement.spacedBy(4.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        filters.forEach { filter ->
            AssistChip(
                onClick = { onRemoveFilter(filter) },
                label = { Text(filter) },
                trailingIcon = { Icon(Icons.Default.Close, contentDescription = "Remover", modifier = Modifier.size(16.dp)) }
            )
        }
        TextButton(onClick = onClearAll) { Text("Limpar") }
    }
}

@Composable
private fun SortDropdown(current: SortOption, onSelect: (SortOption) -> Unit, onDismiss: () -> Unit) {
    DropdownMenu(expanded = true, onDismissRequest = onDismiss) {
        SortOption.values().forEach { option ->
            DropdownMenuItem(
                text = { Text(option.label) },
                leadingIcon = { if (current == option) Icon(Icons.Default.Check, contentDescription = null) },
                onClick = { onSelect(option); onDismiss() }
            )
        }
    }
}

enum class SortOption(val label: String, val apiValue: String) {
    Name("Nome A-Z", "SortName"),
    DateAdded("Data de Adição", "DateCreated"),
    ReleaseDate("Data de Lançamento", "PremiereDate"),
    Runtime("Duração", "Runtime"),
    CommunityRating("Avaliação", "CommunityRating"),
    Random("Aleatório", "Random"),
    PlayCount("Mais Assistidos", "PlayCount"),
    LastPlayed("Assistido Recentemente", "DatePlayed"),
}

@Composable
private fun EmptyLibraryState(
    hasFilters: Boolean,
    onClearFilters: () -> Unit,
) {
    Box(modifier = Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Icon(
                Icons.Default.FolderOpen,
                contentDescription = null,
                modifier = Modifier.size(64.dp),
                tint = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(
                text = if (hasFilters) "Nenhum item corresponde aos filtros selecionados" else "Nenhum item nesta biblioteca",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            if (hasFilters) {
                Button(onClick = onClearFilters) {
                    Text("Limpar filtros")
                }
            }
        }
    }
}
