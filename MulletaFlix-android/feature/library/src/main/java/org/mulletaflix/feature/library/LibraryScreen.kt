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
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.zIndex
import androidx.compose.foundation.focusable
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import android.content.res.Configuration
import kotlin.math.roundToInt
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.LocalLifecycleOwner
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
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
    val lifecycleOwner = LocalLifecycleOwner.current
    val isTelevision = (LocalConfiguration.current.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
        Configuration.UI_MODE_TYPE_TELEVISION
    val isTablet = LocalConfiguration.current.smallestScreenWidthDp >= 600 && !isTelevision

    // A library must be populated as soon as its destination is entered. The
    // TV refresh loop is intentionally periodic, so relying on it for the
    // first request leaves a newly opened screen empty until the first tick.
    LaunchedEffect(libraryId) {
        viewModel.loadLibrary(libraryId)
    }

    TvRefreshEffect(
        lifecycleOwner = lifecycleOwner,
        refreshIntervalMillis = libraryAutoRefreshIntervalMillis(isTelevision),
        refreshImmediately = libraryRefreshImmediatelyOnResume(isTelevision),
        onRefresh = { viewModel.refreshIfIdle(libraryId) },
    )

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(state.libraryName) },
                navigationIcon = {
                    MulletaFlixTopBarAction(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar") }
                },
                actions = {
                    MulletaFlixTopBarAction(
                        onClick = { viewModel.loadLibrary(libraryId) },
                        busy = state.isLoading,
                        busyContentDescription = "Atualizar biblioteca",
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar biblioteca")
                    }
                    // View toggle (grid / list)
                    MulletaFlixTopBarAction(onClick = viewModel::toggleView) {
                        Icon(if (state.isGridView) Icons.AutoMirrored.Filled.ViewList else Icons.Default.GridView, contentDescription = "Alternar visualização")
                    }
                    // Sort
                    MulletaFlixTopBarAction(onClick = viewModel::showSortMenu) {
                        Icon(
                            Icons.AutoMirrored.Filled.Sort,
                            contentDescription = "Ordenar: ${state.sortBy.label}, ${state.sortOrder.label}",
                        )
                    }
                    // Filter
                    MulletaFlixTopBarAction(onClick = viewModel::showFilterMenu) {
                        Icon(Icons.Default.FilterList, contentDescription = "Filtrar")
                    }
                }
            )
        }
    ) { padding ->
        PullToRefreshBox(
            isRefreshing = state.isRefreshing,
            onRefresh = { viewModel.loadLibrary(libraryId) },
            modifier = Modifier.fillMaxSize().padding(padding),
        ) {
        BoxWithConstraints(modifier = Modifier.fillMaxSize()) {
        val viewportWidthDp = maxWidth.value.roundToInt()
        Box(modifier = Modifier.fillMaxSize()) {
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
                val columns = if (state.isGridView) {
                    val tvColumns = libraryGridColumns(
                        widthDp = viewportWidthDp,
                        density = state.gridDensity,
                        isTelevision = isTelevision,
                    )
                    if (tvColumns > 0) {
                        GridCells.Fixed(tvColumns)
                    } else {
                        GridCells.Adaptive(
                            minSize = libraryGridMinSizeDp(
                                state.gridDensity,
                                isTablet = isTablet,
                            ).dp,
                        )
                    }
                } else {
                    GridCells.Fixed(1)
                }

                LazyVerticalGrid(
                    state = rememberLibraryGridScrollState(),
                    columns = columns,
                    contentPadding = PaddingValues(8.dp),
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    // Active filters summary
                    if (loadError != null) {
                        item(span = { GridItemSpan(maxLineSpan) }) {
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
                        item(span = { GridItemSpan(maxLineSpan) }) {
                            ActiveFiltersRow(
                                filters = state.activeFilters,
                                onRemoveFilter = viewModel::removeFilter,
                                onClearAll = viewModel::clearFilters
                            )
                        }
                    }

                    items(
                        items = state.items,
                        key = { item -> item.id },
                    ) { item ->
                        if (state.isGridView) {
                            MediaCard(
                                title = item.name,
                                imageUrl = item.primaryImageUrl,
                                metadata = item.cardMetadata(),
                                shape = libraryCardShape(item),
                                progress = item.playbackProgressFraction(),
                                isWatched = item.isPlayed,
                                isFavorite = item.isFavorite,
                                 unplayedCount = item.unplayedItemCount ?: 0,
                                 qualityBadge = when { item.has4K -> "4K"; item.hasHD -> "HD"; else -> null },
                                 focusFriendly = isTelevision,
                                 onClick = { onItemClick(item.id) },
                                modifier = Modifier.fillMaxWidth()
                            )
                        } else {
                            LibraryListRow(
                                item = item,
                                focusFriendly = isTelevision,
                                onClick = { onItemClick(item.id) },
                            )
                        }
                    }

                    // Load more trigger
                    if (state.hasMore) {
                        item(span = { GridItemSpan(maxLineSpan) }) {
                            LaunchedEffect(Unit) { viewModel.loadMore() }
                            Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                                CircularProgressIndicator(modifier = Modifier.padding(16.dp))
                            }
                        }
                    }

                    item(span = { GridItemSpan(maxLineSpan) }) {
                        Spacer(modifier = Modifier.height(80.dp))
                    }
                }
            }

            if (state.isOffline) {
                LibraryOfflineBanner(
                    onRetry = { viewModel.loadLibrary(libraryId) },
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(12.dp)
                        .zIndex(1f),
                )
            }

            // Sort dropdown
            if (state.showSortMenu) {
                SortDropdown(
                    current = state.sortBy,
                    currentOrder = state.sortOrder,
                    onApply = viewModel::setSort,
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
    }
}

@Composable
internal fun LibraryOfflineBanner(
    onRetry: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Card(
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.errorContainer,
        ),
        modifier = modifier.fillMaxWidth().widthIn(max = 640.dp),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Icon(
                Icons.Default.CloudOff,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onErrorContainer,
            )
            Text(
                text = "Sem conexão. A biblioteca será atualizada quando a rede voltar.",
                color = MaterialTheme.colorScheme.onErrorContainer,
                style = MaterialTheme.typography.bodySmall,
                modifier = Modifier.weight(1f),
            )
            TextButton(onClick = onRetry) {
                Text("Tentar novamente")
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
private fun LibraryListRow(item: MediaItem, focusFriendly: Boolean, onClick: () -> Unit) {
    var isFocused by remember { mutableStateOf(false) }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .then(
                if (focusFriendly) {
                    // No `focusable()`: the `clickable` below already provides a
                    // focus target, and a second one on the same node swallowed the
                    // remote's first press, so a library row needed two clicks.
                    Modifier.onFocusChanged { isFocused = it.isFocused }
                } else {
                    Modifier
                },
            )
            .then(
                if (focusFriendly && isFocused) {
                    Modifier.border(2.dp, MaterialTheme.colorScheme.primary, MaterialTheme.shapes.medium)
                } else {
                    Modifier
                },
            )
            .clickable(onClick = onClick)
            .semantics(mergeDescendants = true) {
                role = Role.Button
                contentDescription = "Abrir ${item.name}"
            }
            .padding(4.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        MediaCard(
            title = item.name,
            imageUrl = item.primaryImageUrl,
            metadata = item.cardMetadata(),
            shape = libraryCardShape(item),
            isWatched = item.isPlayed,
            isFavorite = item.isFavorite,
            unplayedCount = item.unplayedItemCount ?: 0,
            isClickable = false,
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

internal fun libraryCardShape(item: MediaItem): MediaCardShape =
    if (item.type.usesPosterArtwork()) MediaCardShape.Portrait else MediaCardShape.Landscape

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
internal fun SortDropdown(
    current: SortOption,
    currentOrder: SortOrder,
    onApply: (SortOption, SortOrder) -> Unit,
    onDismiss: () -> Unit,
) {
    var selectedOption by remember(current) { mutableStateOf(current) }
    var selectedOrder by remember(currentOrder) { mutableStateOf(currentOrder) }

    DropdownMenu(expanded = true, onDismissRequest = onDismiss) {
        SortOption.values().forEach { option ->
            DropdownMenuItem(
                text = { Text(option.label) },
                leadingIcon = { if (selectedOption == option) Icon(Icons.Default.Check, contentDescription = null) },
                // `DropdownMenuItem` do material3 1.4.0 não tem parâmetro `selected`,
                // então a única marca do campo ativo era um visto sem descrição: o
                // leitor de tela lia os nomes e nunca dizia qual estava escolhido.
                modifier = Modifier.semantics { selected = selectedOption == option },
                onClick = { selectedOption = option },
            )
        }
        HorizontalDivider()
        SortOrder.values().forEach { order ->
            DropdownMenuItem(
                text = { Text(order.label) },
                leadingIcon = {
                    Icon(
                        if (order == SortOrder.Ascending) Icons.Default.ArrowUpward else Icons.Default.ArrowDownward,
                        contentDescription = null,
                    )
                },
                trailingIcon = { if (selectedOrder == order) Icon(Icons.Default.Check, contentDescription = null) },
                modifier = Modifier.semantics { selected = selectedOrder == order },
                onClick = { selectedOrder = order },
            )
        }
        HorizontalDivider()
        DropdownMenuItem(
            text = { Text("Aplicar") },
            leadingIcon = { Icon(Icons.Default.Check, contentDescription = null) },
            onClick = { onApply(selectedOption, selectedOrder); onDismiss() },
        )
    }
}

/**
 * Sort options offered in the library menu.
 *
 * The label and the server code come from the shared [LibrarySortField], so the
 * settings screen — which displays and re-persists the same value — cannot
 * disagree with this menu. When the two were separate lists, "Data de Adição"
 * here and "Data de adição" there never matched, so Settings showed "Nome"
 * while the library was ordered differently and confirming that value
 * overwrote the real choice.
 */
enum class SortOption(val label: String, val apiValue: String) {
    Name(LibrarySortField.Name.label, LibrarySortField.Name.code),
    DateAdded(LibrarySortField.DateAdded.label, LibrarySortField.DateAdded.code),
    ReleaseDate(LibrarySortField.ReleaseDate.label, LibrarySortField.ReleaseDate.code),
    Runtime(LibrarySortField.Runtime.label, LibrarySortField.Runtime.code),
    CommunityRating(LibrarySortField.CommunityRating.label, LibrarySortField.CommunityRating.code),
    Random(LibrarySortField.Random.label, LibrarySortField.Random.code),
    PlayCount(LibrarySortField.PlayCount.label, LibrarySortField.PlayCount.code),
    LastPlayed(LibrarySortField.LastPlayed.label, LibrarySortField.LastPlayed.code),
}

enum class SortOrder(val label: String, val apiValue: String) {
    Ascending("Ascendente", "Ascending"),
    Descending("Descendente", "Descending"),
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
