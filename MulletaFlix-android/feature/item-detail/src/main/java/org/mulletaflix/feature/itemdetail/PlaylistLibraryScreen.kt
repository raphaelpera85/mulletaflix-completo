package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.VideoLibrary
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil.compose.AsyncImage
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.designsystem.components.remoteFocusRing
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.model.displayYearRange
import org.mulletaflix.domain.model.primaryImageUrl

@OptIn(androidx.compose.material3.ExperimentalMaterial3Api::class)
@Composable
fun PlaylistLibraryScreen(
    onBack: () -> Unit,
    onItemClick: (String) -> Unit,
    onPlay: (String) -> Unit,
    viewModel: PlaylistLibraryViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Minhas playlists") },
                navigationIcon = { MulletaFlixTopBarAction(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Voltar") } },
                actions = { IconButton(onClick = viewModel::refresh) { Icon(Icons.Default.Refresh, "Atualizar playlists") } },
            )
        },
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding)) {
            if (state.playlists.isNotEmpty()) {
                androidx.compose.foundation.lazy.LazyRow(
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    items(state.playlists, key = Playlist::id) { playlist ->
                        PlaylistChip(playlist, selected = playlist.id == state.selectedPlaylist?.id) { viewModel.selectPlaylist(playlist) }
                    }
                }
            }
            val playlistError = state.error
            val itemsError = state.itemsError
            if (state.playlists.isEmpty() && playlistError != null) {
                PlaylistError(playlistError, onRetry = viewModel::loadPlaylists)
            } else if ((state.isLoadingPlaylists && state.playlists.isEmpty()) || (state.isLoadingItems && state.items.isEmpty())) {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            } else if (state.playlists.isEmpty()) {
                EmptyState("Nenhuma playlist encontrada", "Crie uma playlist nos detalhes de um título.")
            } else if (state.items.isEmpty() && itemsError != null) {
                PlaylistError(itemsError, onRetry = viewModel::retryItems)
            } else if (state.items.isEmpty()) {
                EmptyState("Playlist vazia", "Adicione títulos usando a opção de playlist nos detalhes.")
            } else {
                LazyColumn(Modifier.fillMaxSize(), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    if (playlistError != null) {
                        item(key = "playlists-retry") {
                            PlaylistError(playlistError, onRetry = viewModel::loadPlaylists)
                        }
                    }
                    item {
                        Text(
                            "${state.selectedPlaylist?.name.orEmpty()} · ${state.totalItems} títulos",
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp),
                        )
                    }
                    items(state.items, key = { it.id }) { media ->
                        PlaylistMediaRow(media, onClick = { onItemClick(media.id) }, onPlay = { onPlay(media.id) })
                    }
                    if (itemsError != null) {
                        item(key = "items-retry") {
                            PlaylistError(itemsError, onRetry = viewModel::retryItems)
                        }
                    } else if (state.isLoadingItems) {
                        item(key = "items-loading") {
                            Box(
                                Modifier.fillMaxWidth().padding(16.dp),
                                contentAlignment = Alignment.Center,
                            ) {
                                CircularProgressIndicator(
                                    Modifier.size(20.dp).semantics {
                                        contentDescription = "Carregando mais títulos"
                                    },
                                    strokeWidth = 2.dp,
                                )
                            }
                        }
                    } else if (state.hasMoreItems) {
                        item {
                            TextButton(onClick = viewModel::loadNextPage, modifier = Modifier.fillMaxWidth().padding(12.dp)) {
                                if (state.isLoadingItems) CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
                                else Text("Carregar mais")
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun PlaylistError(message: String, onRetry: () -> Unit) {
    Column(Modifier.fillMaxWidth().padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        Text(message, color = MaterialTheme.colorScheme.error)
        TextButton(onClick = onRetry) { Text("Tentar novamente") }
    }
}

@Composable
private fun PlaylistChip(playlist: Playlist, selected: Boolean, onClick: () -> Unit) {
    Surface(
        modifier = Modifier.remoteFocusRing(shape = RoundedCornerShape(24.dp)).clickable(onClick = onClick),
        shape = RoundedCornerShape(24.dp),
        color = if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceVariant,
    ) {
        Row(Modifier.padding(horizontal = 16.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Default.VideoLibrary, null, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(8.dp))
            Text(playlist.name, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
    }
}

@Composable
private fun PlaylistMediaRow(media: org.mulletaflix.domain.model.MediaItem, onClick: () -> Unit, onPlay: () -> Unit) {
    val image = resolveMediaUrl(LocalMulletaFlixServerUrl.current, media.primaryImageUrl, LocalMulletaFlixAccessToken.current)
    Row(
        Modifier.fillMaxWidth().clickable(onClick = onClick).remoteFocusRing(shape = RoundedCornerShape(12.dp)).padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        AsyncImage(image, contentDescription = "Capa de ${media.name}", modifier = Modifier.size(width = 64.dp, height = 96.dp).clip(RoundedCornerShape(6.dp)), contentScale = ContentScale.Crop)
        Column(Modifier.weight(1f).padding(horizontal = 14.dp)) {
            Text(media.name, style = MaterialTheme.typography.titleSmall, maxLines = 2, overflow = TextOverflow.Ellipsis)
            val metadata = listOfNotNull(media.displayYearRange(), media.type.name).joinToString(" · ")
            Text(metadata, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
        }
        IconButton(onClick = onPlay, modifier = Modifier.remoteFocusRing(shape = RoundedCornerShape(50))) { Icon(Icons.Default.PlayArrow, "Reproduzir ${media.name}") }
    }
}

@Composable
private fun EmptyState(title: String, message: String) {
    Box(Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Icon(Icons.Default.VideoLibrary, null, tint = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.size(40.dp))
            Spacer(Modifier.height(12.dp))
            Text(title, style = MaterialTheme.typography.titleMedium)
            Text(message, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
