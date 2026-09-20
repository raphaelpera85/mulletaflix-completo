package org.mulletaflix.feature.itemdetail

import android.content.Intent
import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.PlaylistAdd
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.platform.LocalContext
import androidx.hilt.navigation.compose.hiltViewModel
import coil.compose.AsyncImage
import org.mulletaflix.domain.model.*
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken

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
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val serverUrl = LocalMulletaFlixServerUrl.current

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
                    onPlay = { onPlay(playbackTargetId(item, state.episodes)) },
                    playEnabled = canPlayItem(item, state.episodes),
                     onFavorite = { viewModel.toggleFavorite() },
                     onMarkWatched = { viewModel.toggleWatched() },
                     onDownload = { viewModel.downloadItem() },
                     isDownloadPreparing = state.isPreparingDownload,
                     isFavoriteUpdating = state.isFavoriteUpdating,
                     isWatchedUpdating = state.isWatchedUpdating,
                    onPlaylist = { viewModel.openPlaylistPicker() },
                    onShare = {
                        val text = buildItemShareText(item.name, item.id, serverUrl)
                        val shareIntent = Intent(Intent.ACTION_SEND).apply {
                            type = "text/plain"
                            putExtra(Intent.EXTRA_TEXT, text)
                            putExtra(Intent.EXTRA_TITLE, item.name)
                        }
                        context.startActivity(Intent.createChooser(shareIntent, "Compartilhar título"))
                    },
                    isLoading = state.isLoading
                )

                // ── Metadata pills ────────────────────────────────────────────
                MetadataPills(item = item)

                state.downloadMessage?.let { message ->
                    Text(message, color = MaterialTheme.colorScheme.secondary, modifier = Modifier.padding(horizontal = 20.dp, vertical = 8.dp))
                }
                state.interactionMessage?.let { message ->
                    Text(message, color = MaterialTheme.colorScheme.secondary, modifier = Modifier.padding(horizontal = 20.dp, vertical = 8.dp))
                }
                state.playlistMessage?.let { message ->
                    Text(message, color = MaterialTheme.colorScheme.secondary, modifier = Modifier.padding(horizontal = 20.dp, vertical = 8.dp))
                }

                // ── Overview ─────────────────────────────────────────────────
                item.overview?.let { overview ->
                    ExpandableOverview(text = overview)
                }

                // ── Series-specific: Season selector + Episodes ───────────────
                // Also rendered for Season/Episode items: the ViewModel resolves
                // the parent series and its seasons so the episode stays inside
                // the series context instead of living as a loose item.
                if (item.type == MediaItemType.Series || state.seasons.isNotEmpty()) {
                    SeriesSection(
                        seasons = state.seasons,
                        episodes = state.episodes,
                        selectedSeasonIndex = state.selectedSeasonIndex,
                        onSeasonSelect = viewModel::selectSeason,
                        onEpisodePlay = onPlay,
                        onEpisodeClick = onItemClick,
                        isLoading = state.isLoadingSeasons
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

        if (state.isPlaylistDialogVisible) {
            PlaylistPickerDialog(
                playlists = state.playlists,
                isLoading = state.isPlaylistLoading,
                message = state.playlistMessage,
                onDismiss = viewModel::closePlaylistPicker,
                onPlaylistSelected = viewModel::addToPlaylist,
                onCreate = viewModel::createPlaylist,
            )
        }

        // Loading overlay
        if (state.isLoading && state.item == null) {
            CircularProgressIndicator(
                modifier = Modifier.align(Alignment.Center),
                color = MaterialTheme.colorScheme.secondary
            )
        }

        // Error
        state.error?.let { err ->
            Card(
                modifier = Modifier.align(Alignment.Center).padding(24.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
            ) {
                Column(modifier = Modifier.padding(16.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(err, color = MaterialTheme.colorScheme.onErrorContainer)
                    TextButton(onClick = { viewModel.loadItem(itemId) }) {
                        Text("Tentar novamente")
                    }
                }
            }
        }
    }
}

@Composable
private fun DetailHero(
    item: MediaItem,
    onBack: () -> Unit,
    onPlay: () -> Unit,
    playEnabled: Boolean,
    onFavorite: () -> Unit,
    onMarkWatched: () -> Unit,
    onDownload: () -> Unit,
    isDownloadPreparing: Boolean,
    isFavoriteUpdating: Boolean,
    isWatchedUpdating: Boolean,
    onPlaylist: () -> Unit,
    onShare: () -> Unit,
    isLoading: Boolean,
) {
    val serverUrl = LocalMulletaFlixServerUrl.current
    val accessToken = LocalMulletaFlixAccessToken.current
    Box(modifier = Modifier.fillMaxWidth().height(420.dp)) {
        // Backdrop
        AsyncImage(
            model = resolveMediaUrl(serverUrl, item.backdropImageUrl, accessToken),
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
            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar", tint = Color.White)
        }

        // Bottom content: keep the complete poster visible beside the metadata.
        Row(
            modifier = Modifier
                .align(Alignment.BottomStart)
                .fillMaxWidth()
                .padding(20.dp),
            verticalAlignment = Alignment.Bottom,
        ) {
            AsyncImage(
                model = resolveMediaUrl(serverUrl, item.primaryImageUrl, accessToken),
                contentDescription = "${item.name} — capa",
                contentScale = ContentScale.Fit,
                modifier = Modifier
                    .width(112.dp)
                    .aspectRatio(2f / 3f)
                    .clip(RoundedCornerShape(8.dp))
                    .background(Color.Black.copy(alpha = 0.45f)),
            )
            Spacer(modifier = Modifier.width(16.dp))
            Column(modifier = Modifier.weight(1f)) {
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
                Row(
                    modifier = Modifier.horizontalScroll(rememberScrollState()),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                // Play
                Button(
                    onClick = onPlay,
                    enabled = playEnabled,
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.secondary)
                ) {
                    Icon(Icons.Default.PlayArrow, contentDescription = null)
                    Spacer(Modifier.width(4.dp))
                    Text(if ((item.playbackPositionTicks ?: 0L) > 0L) "Continuar" else "Reproduzir")
                }

                // Favorite
                 IconButton(
                     onClick = onFavorite,
                     enabled = !isFavoriteUpdating,
                     modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                 ) {
                     if (isFavoriteUpdating) {
                         CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp, color = Color.White)
                     } else {
                         Icon(
                             if (item.isFavorite) Icons.Default.Favorite else Icons.Outlined.FavoriteBorder,
                             contentDescription = if (item.isFavorite) "Remover dos favoritos" else "Adicionar aos favoritos",
                             tint = if (item.isFavorite) Color(0xFFE53935) else Color.White
                         )
                     }
                 }

                // Mark watched
                 IconButton(
                     onClick = onMarkWatched,
                     enabled = !isWatchedUpdating,
                     modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                 ) {
                     if (isWatchedUpdating) {
                         CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp, color = Color.White)
                     } else {
                         Icon(
                             if (item.isPlayed) Icons.Default.CheckCircle else Icons.Outlined.CheckCircleOutline,
                             contentDescription = if (item.isPlayed) "Marcar como não assistido" else "Marcar como assistido",
                             tint = if (item.isPlayed) Color(0xFF4CAF50) else Color.White
                         )
                     }
                 }

                // Download for offline playback
                IconButton(
                    onClick = onDownload,
                    enabled = !isDownloadPreparing,
                    modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                ) {
                    if (isDownloadPreparing) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(22.dp),
                            strokeWidth = 2.dp,
                            color = Color.White,
                        )
                    } else {
                        Icon(Icons.Default.Download, contentDescription = "Baixar para assistir offline", tint = Color.White)
                    }
                }

                IconButton(
                    onClick = onPlaylist,
                    modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                ) {
                    Icon(Icons.AutoMirrored.Filled.PlaylistAdd, contentDescription = "Adicionar à playlist", tint = Color.White)
                }

                IconButton(
                    onClick = onShare,
                    modifier = Modifier.background(Color.White.copy(0.15f), CircleShape)
                ) {
                    Icon(Icons.Default.Share, contentDescription = "Compartilhar título", tint = Color.White)
                }
                }
            }
        }
    }
}

@Composable
private fun PlaylistPickerDialog(
    playlists: List<Playlist>,
    isLoading: Boolean,
    message: String?,
    onDismiss: () -> Unit,
    onPlaylistSelected: (Playlist) -> Unit,
    onCreate: (String) -> Unit,
) {
    var newName by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Adicionar à playlist") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                if (isLoading) {
                    CircularProgressIndicator(modifier = Modifier.align(Alignment.CenterHorizontally))
                } else if (playlists.isEmpty()) {
                    Text("Nenhuma playlist encontrada.")
                } else {
                    playlists.forEach { playlist ->
                        TextButton(onClick = { onPlaylistSelected(playlist) }, modifier = Modifier.fillMaxWidth()) {
                            Text(playlist.name, modifier = Modifier.weight(1f), textAlign = androidx.compose.ui.text.style.TextAlign.Start)
                            Icon(Icons.Default.Add, contentDescription = null)
                        }
                    }
                }
                OutlinedTextField(
                    value = newName,
                    onValueChange = { newName = it },
                    label = { Text("Nova playlist") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                message?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            }
        },
        confirmButton = {
            TextButton(onClick = { onCreate(newName) }, enabled = newName.isNotBlank() && !isLoading) { Text("Criar e adicionar") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } }
    )
}

@Composable
internal fun MetadataPills(item: MediaItem) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .horizontalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 8.dp),
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
                Chip(text = genre)
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
            color = MaterialTheme.colorScheme.secondary,
            modifier = Modifier.padding(top = 4.dp)
        )
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
                        model = resolveMediaUrl(LocalMulletaFlixServerUrl.current, person.primaryImageTag?.let { "Persons/${person.id}/Images/Primary?tag=$it" }, LocalMulletaFlixAccessToken.current),
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
                MediaCard(title = item.name, imageUrl = item.primaryImageUrl, metadata = item.cardMetadata(), shape = MediaCardShape.Portrait, onClick = { onItemClick(item.id) }, modifier = Modifier.width(110.dp))
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
                MediaCard(title = item.name, imageUrl = item.primaryImageUrl, metadata = item.cardMetadata(), shape = MediaCardShape.Landscape, onClick = { onPlay(item.id) }, modifier = Modifier.width(200.dp))
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
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
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

private val MediaItem.runtimeMinutes: Int? get() = runtimeTicks?.div(600_000_000L)?.toInt()?.takeIf { it > 0 }
