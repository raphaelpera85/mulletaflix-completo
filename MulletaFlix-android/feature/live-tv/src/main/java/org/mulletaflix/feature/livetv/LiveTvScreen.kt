package org.mulletaflix.feature.livetv

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LiveTvScreen(
    onChannelPlay: (String) -> Unit,
) {
    val channels = remember {
        listOf(
            MediaItem(id = "ch-1", name = "Mulleta Cinema HD", type = MediaItemType.LiveTvChannel, overview = "O Poderoso Chefão (Ao Vivo)"),
            MediaItem(id = "ch-2", name = "Mulleta Series 4K", type = MediaItemType.LiveTvChannel, overview = "Breaking Bad S03E05"),
            MediaItem(id = "ch-3", name = "Mulleta Notícias 24h", type = MediaItemType.LiveTvChannel, overview = "Edição das 14h"),
            MediaItem(id = "ch-4", name = "Mulleta Esportes", type = MediaItemType.LiveTvChannel, overview = "Fórmula 1 - GP de Interlagos"),
            MediaItem(id = "ch-5", name = "Mulleta Documentários", type = MediaItemType.LiveTvChannel, overview = "Planeta Terra II"),
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("TV Ao Vivo & EPG") },
                actions = {
                    IconButton(onClick = { /* Guia EPG */ }) {
                        Icon(Icons.Default.CalendarMonth, contentDescription = "Guia EPG")
                    }
                }
            )
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            item {
                Text(
                    text = "Canais em Destaque",
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.padding(bottom = 8.dp)
                )
            }

            items(channels) { channel ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clickable { onChannelPlay(channel.id) },
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Surface(
                            shape = MaterialTheme.shapes.small,
                            color = MaterialTheme.colorScheme.primaryContainer,
                            modifier = Modifier.size(48.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center) {
                                Icon(Icons.Default.LiveTv, contentDescription = null, tint = MaterialTheme.colorScheme.onPrimaryContainer)
                            }
                        }
                        Spacer(modifier = Modifier.width(16.dp))
                        Column(modifier = Modifier.weight(1f)) {
                            Text(channel.name, style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.onSurface)
                            Text(channel.overview ?: "Sem informações de guia", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        IconButton(onClick = { onChannelPlay(channel.id) }) {
                            Icon(Icons.Default.PlayCircleOutline, contentDescription = "Assistir", tint = MaterialTheme.colorScheme.primary)
                        }
                    }
                }
            }
        }
    }
}
