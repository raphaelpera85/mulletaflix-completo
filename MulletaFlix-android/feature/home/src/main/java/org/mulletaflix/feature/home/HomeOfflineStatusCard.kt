package org.mulletaflix.feature.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import java.text.DateFormat
import java.util.Date

@Composable
internal fun HomeOfflineStatusCard(
    cachedAtEpochMillis: Long?,
    resumeCached: Boolean = false,
    favoritesCached: Boolean = false,
    downloadsAvailable: Boolean = true,
) {
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
                text = cachedAtEpochMillis?.let { savedAt ->
                    val cachedNames = buildList {
                        if (resumeCached) add("Continuar Assistindo")
                        if (favoritesCached) add("Favoritos")
                    }.joinToString(" e ")
                    "Sem conexão · $cachedNames em cache desde " +
                        DateFormat.getDateTimeInstance().format(Date(savedAt)) +
                        if (downloadsAvailable) ". Apenas mídias baixadas podem ser reproduzidas offline." else "."
                } ?: if (downloadsAvailable) {
                    "Você está offline e não há conteúdo da Home salvo. Acesse Downloads para reproduzir mídias baixadas."
                } else {
                    "Você está offline e não há conteúdo da Home salvo."
                },
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
