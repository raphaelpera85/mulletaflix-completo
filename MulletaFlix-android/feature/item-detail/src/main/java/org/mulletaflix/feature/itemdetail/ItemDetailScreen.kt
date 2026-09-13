package org.mulletaflix.feature.itemdetail

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import coil.compose.AsyncImage
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MediaCardShape

/**
 * Item detail screen covering all content types:
 *  - Movies: full metadata, cast, similar, extras
 *  - Series: season selector, episode list
 *  - Episodes: series context, next episode
 *  - Music/Albums: track list, lyrics button
 *  - Books: cover + reader button
 */
@Composable
fun ItemDetailScreen(
    itemId: String,
    onPlay: (String) -> Unit,
    onItemClick: (String) -> Unit,
    onBack: () -> Unit,
    viewModel: ItemDetailViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsState()

    LaunchedEffect(itemId) { viewModel.loadItem(itemId) }

    val scrollState = rememberScrollState()

    Box(modifier = Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {

        state.item?.let { item ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(scrollState)
            ) {
                // ── Backdrop / Hero ──────────────────────────────────────────
                DetailHero(
                    item = item,
                    onBack = onBack,
                    onPlay = { onPlay(item.id) },
                    onFavorite = { viewModel.toggleFavorite() },
                    onMarkWatched = { viewModel.toggleWatched() },
                    onAddToPlaylist = { /* TODO */ },
                    isLoading = state.isLoading
                )

                // ── Metadata pills ────────────────────────────────────────────
                MetadataPills(item = item)

                // ── Overview ─────────────────────────────────────────────────
                item.overview?.let { overview ->
                    ExpandableOverview(text = overview)
                }

                // ── Series-specific: Season selector + Episodes ───────────────
                if (item.type == MediaItemType.Series) {
                    SeriesSection(
                        seasons = state.seasons,
                        episodes = state.episodes,
                        selectedSeasonIndex = state.selectedSeasonIndex,
                        onSeasonSelect = viewModel::selectSeason,
                        onEpisodePlay = onPlay,
                        onEpisodeClick = onItemClick
                    )
                }

                // ── Music-specific: Track list ────────────────────────────────
                if (item.type == MediaItemType.MusicAlbum) {
                    TrackListSection(
                        tracks = state.episodes, // episodes reused for tracks
                        onTrackPlay = onPlay
                    )
                }

                // ── Cast & Crew ──────────────────────────────────────────────
                if (item.people.isNotEmpty()) {
                    CastSection(people = item.people, onPersonClick = onItemClick)
                }

                // ── Similar Items ────────────────────────────────────────────
                if (state.similarItems.isNotEmpty()) {
                    SimilarSection(items = state.similarItems, onItemClick = onItemClick)
                }

                // ── Special Features ─────────────────────────────────────────
                if (state.specialFeatures.isNotEmpty()) {
                    SpecialFeaturesSection(items = state.specialFeatures, onPlay = onPlay)
                }

                // ── Media Technical Info ─────────────────────────────────────
                MediaInfoSection(item = item)

                Spacer(modifier = Modifier.height(80.dp))
            }
        }

        // Loading overlay
        if (state.isLoading && state.item == null) {
            CircularProgressIndicator(
                modifier = Modifier.align(Alignment.Center),
                color = Color(0xFF00A4DC)
            )
        }

        // Error
        state.error?.let { err ->
            Text(
                text = err,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.align(Alignment.Center).padding(16.dp)
            )
        }
    }
}

@Composable
private fun DetailHero(
    item: MediaItem,
    onBack: () -> Unit,
    onPlay: () -> Unit,
    onFavorite: () -> Unit,
    onMarkWatched: () -> Unit,
    onAddToPlaylist: () -> Unit,
    isLoading: Boolean,
) {
    Box(modifier = Modifier.fillMaxWidth().height(420.dp)) {
        // Backdrop
        AsyncImage(
            model = item.backdropImageUrl,
            contentDescription = null,
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize()
        )

        // Gradient
        Box(
            modifier = Modifier.fillMaxSize().background(
                Brush.verticalGradient(
                    colors = listOf(Color.Black.copy(0.3f), Color.Transparent, Color.Black.copy(0.9f))
                )
            )
        )

        // Back button
        IconButton(
            onClick = onBack,
            modifier = Modifier.align(Alignment.TopStart).padding(16.dp)
                .background(Color.Black.copy(0.4f), CircleShape)
        ) {
            Icon(Icons.Default.ArrowBack, contentDescription = "Voltar", tint = Color.White)
        }

        // Bottom content
        Column(
            modifier = Modifier.align(Alignment.BottomStart).padding(20.dp)
        ) {
            // Title
            Text(
                text = item.name,
                style = MaterialTheme.typography.headlineSmall,
                color = Color.White,
                fontWeight = FontWeight.Bold
            )

            // Series name for episodes
            item.seriesName?.let {
                Text(it, style = MaterialTheme.typography.bodySmall, color = Color.White.copy(0.7f))
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Action buttons row
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                // Play
                Button(
                    onClick = onPlay,
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF00A4DC))
                ) {
                    Icon(Icons.Default.PlayArrow, contentDescription = null)
                    Spacer(Modifier.width(4.dp))
                    Text(if (item.playbackPositionTicks != null && item.playbackPositionTicks > 0) "Continuar" else "Reproduzir")
                }

                // Favorite
                IconButton(
                    onClick = onFavorite,
                    modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                ) {
                    Icon(
                        if (item.isFavorite) Icons.Default.Favorite else Icons.Outlined.FavoriteBorder,
                        contentDescription = "Favorito",
                        tint = if (item.isFavorite) Color(0xFFE53935) else Color.White
                    )
                }

                // Mark watched
                IconButton(
                    onClick = onMarkWatched,
                    modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                ) {
                    Icon(
                        if (item.isPlayed) Icons.Default.CheckCircle else Icons.Outlined.CheckCircleOutline,
                        contentDescription = "Marcar como assistido",
                        tint = if (item.isPlayed) Color(0xFF4CAF50) else Color.White
                    )
                }

                // More options
                IconButton(
                    onClick = onAddToPlaylist,
                    modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                ) {
                    Icon(Icons.Default.MoreVert, contentDescription = "Mais opções", tint = Color.White)
                }
            }
        }
    }
}

@Composable
private fun MetadataPills(item: MediaItem) {
    Row(
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        item.year?.let { Chip(text = "$it") }
        item.officialRating?.let { Chip(text = it, outlined = true) }
        item.runtimeMinutes?.let { Chip(text = "$it min") }
        item.communityRating?.let { Chip(text = "★ ${String.format("%.1f", it)}") }
        if (item.has4K) Chip(text = "4K", color = Color(0xFFFF9800))
        else if (item.hasHD) Chip(text = "HD", color = Color(0xFF2196F3))
        if (item.hasHdr) Chip(text = "HDR", color = Color(0xFF9C27B0))
        if (item.hasAtmos) Chip(text = "Atmos", color = Color(0xFF3F51B5))
    }
    if (item.genres.isNotEmpty()) {
        LazyRow(
            contentPadding = PaddingValues(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            items(item.genres) { genre ->
                AssistChip(onClick = {}, label = { Text(genre, style = MaterialTheme.typography.labelSmall) })
            }
        }
    }
}

@Composable
private fun ExpandableOverview(text: String) {
    var expanded by remember { mutableStateOf(false) }
    Column(
        modifier = Modifier
            .padding(horizontal = 16.dp, vertical = 8.dp)
            .clickable { expanded = !expanded }
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = if (expanded) Int.MAX_VALUE else 3,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.animateContentSize()
        )
        Text(
            text = if (expanded) "Ver menos" else "Ver mais",
            style = MaterialTheme.typography.labelSmall,
            color = Color(0xFF00A4DC),
            modifier = Modifier.padding(top = 4.dp)
        )
    }
}

@Composable
private fun SeriesSection(
    seasons: List<MediaItem>,
    episodes: List<MediaItem>,
    selectedSeasonIndex: Int,
    onSeasonSelect: (Int) -> Unit,
    onEpisodePlay: (String) -> Unit,
    onEpisodeClick: (String) -> Unit,
) {
    Column(modifier = Modifier.padding(vertical = 8.dp)) {
        // Season tabs
        ScrollableTabRow(
            selectedTabIndex = selectedSeasonIndex,
            containerColor = MaterialTheme.colorScheme.background,
            edgePadding = 16.dp
        ) {
            seasons.forEachIndexed { index, season ->
                Tab(
                    selected = index == selectedSeasonIndex,
                    onClick = { onSeasonSelect(index) },
                    text = { Text(season.name) }
                )
            }
        }
        // Episodes list
        episodes.forEach { ep ->
            EpisodeRow(episode = ep, onPlay = { onEpisodePlay(ep.id) }, onClick = { onEpisodeClick(ep.id) })
        }
    }
}

@Composable
private fun EpisodeRow(episode: MediaItem, onPlay: () -> Unit, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(modifier = Modifier.width(160.dp).aspectRatio(16f / 9f).clip(RoundedCornerShape(6.dp)).background(MaterialTheme.colorScheme.surfaceVariant)) {
            AsyncImage(model = episode.primaryImageUrl, contentDescription = null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
            if (episode.playedPercentage != null && episode.playedPercentage > 0) {
                Box(modifier = Modifier.fillMaxWidth().height(3.dp).align(Alignment.BottomCenter).background(MaterialTheme.colorScheme.surface)) {
                    Box(modifier = Modifier.fillMaxHeight().fillMaxWidth((episode.playedPercentage / 100f).toFloat()).background(Color(0xFF00A4DC)))
                }
            }
            IconButton(onClick = onPlay, modifier = Modifier.align(Alignment.Center).size(40.dp).background(Color.Black.copy(0.5f), CircleShape)) {
                Icon(Icons.Default.PlayArrow, contentDescription = "Reproduzir", tint = Color.White)
            }
        }
        Column(modifier = Modifier.weight(1f).padding(start = 12.dp)) {
            Text("${episode.parentIndexNumber}x${String.format("%02d", episode.indexNumber ?: 0)} ${episode.name}", style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.onSurface, maxLines = 2, overflow = TextOverflow.Ellipsis)
            episode.runtimeMinutes?.let { Text("$it min", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            episode.overview?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(top = 4.dp)) }
        }
    }
}

@Composable
private fun CastSection(people: List<org.mulletaflix.domain.model.PersonInfo>, onPersonClick: (String) -> Unit) {
    Column(modifier = Modifier.padding(vertical = 8.dp)) {
        SectionTitle("Elenco e Equipe")
        LazyRow(contentPadding = PaddingValues(horizontal = 16.dp), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            items(people) { person ->
                Column(
                    modifier = Modifier.width(80.dp).clickable { onPersonClick(person.id) },
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    AsyncImage(
                        model = person.primaryImageTag?.let { "Persons/${person.id}/Images/Primary?tag=$it" },
                        contentDescription = person.name,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.size(64.dp).clip(CircleShape).background(MaterialTheme.colorScheme.surfaceVariant)
                    )
                    Text(person.name, style = MaterialTheme.typography.labelSmall, maxLines = 2, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(top = 4.dp))
                    person.role?.let { Text(it, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                }
            }
        }
    }
}

@Composable
private fun SimilarSection(items: List<MediaItem>, onItemClick: (String) -> Unit) {
    Column(modifier = Modifier.padding(vertical = 8.dp)) {
        SectionTitle("Mais como Este")
        LazyRow(contentPadding = PaddingValues(horizontal = 16.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            items(items) { item ->
                MediaCard(title = item.name, imageUrl = item.primaryImageUrl, shape = MediaCardShape.Portrait, onClick = { onItemClick(item.id) }, modifier = Modifier.width(110.dp))
            }
        }
    }
}

@Composable
private fun SpecialFeaturesSection(items: List<MediaItem>, onPlay: (String) -> Unit) {
    Column(modifier = Modifier.padding(vertical = 8.dp)) {
        SectionTitle("Extras")
        LazyRow(contentPadding = PaddingValues(horizontal = 16.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            items(items) { item ->
                MediaCard(title = item.name, imageUrl = item.primaryImageUrl, shape = MediaCardShape.Landscape, onClick = { onPlay(item.id) }, modifier = Modifier.width(200.dp))
            }
        }
    }
}

@Composable
private fun TrackListSection(tracks: List<MediaItem>, onTrackPlay: (String) -> Unit) {
    Column(modifier = Modifier.padding(vertical = 8.dp)) {
        SectionTitle("Faixas")
        tracks.forEachIndexed { idx, track ->
            Row(
                modifier = Modifier.fillMaxWidth().clickable { onTrackPlay(track.id) }.padding(horizontal = 16.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("${idx + 1}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.width(28.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text(track.name, style = MaterialTheme.typography.bodyMedium)
                    track.runtimeMinutes?.let { Text("$it min", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                }
                Icon(Icons.Default.PlayArrow, contentDescription = "Reproduzir", tint = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Divider(color = MaterialTheme.colorScheme.outlineVariant)
        }
    }
}

@Composable
private fun MediaInfoSection(item: MediaItem) {
    if (item.mediaStreams.isEmpty()) return
    var expanded by remember { mutableStateOf(false) }
    Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth().clickable { expanded = !expanded },
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text("Informações Técnicas", style = MaterialTheme.typography.titleSmall)
            Icon(if (expanded) Icons.Default.ExpandLess else Icons.Default.ExpandMore, contentDescription = null)
        }
        if (expanded) {
            item.mediaStreams.forEach { stream ->
                Text(
                    text = "• ${stream.type.name}: ${stream.displayTitle ?: stream.codec ?: "-"}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = 4.dp)
                )
            }
        }
    }
}

@Composable
private fun SectionTitle(title: String) {
    Text(title, style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.onBackground, modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp))
}

@Composable
private fun Chip(text: String, outlined: Boolean = false, color: Color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.7f)) {
    Surface(
        shape = RoundedCornerShape(4.dp),
        color = if (outlined) Color.Transparent else color.copy(alpha = 0.15f),
        border = if (outlined) BorderStroke(1.dp, color) else null
    ) {
        Text(text, style = MaterialTheme.typography.labelSmall, color = color, modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp))
    }
}

private val MediaItem.primaryImageUrl: String? get() = imageTags[org.mulletaflix.domain.model.ImageType.Primary]?.let { "Items/$id/Images/Primary?tag=$it" }
private val MediaItem.backdropImageUrl: String? get() = backdropImageTags.firstOrNull()?.let { "Items/$id/Images/Backdrop?tag=$it" }
private val MediaItem.runtimeMinutes: Int? get() = runtimeTicks?.div(600_000_000L)?.toInt()?.takeIf { it > 0 }
