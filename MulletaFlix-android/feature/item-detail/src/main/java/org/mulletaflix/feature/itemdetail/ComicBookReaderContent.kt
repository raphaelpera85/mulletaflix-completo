package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

@Composable
internal fun ComicBookReaderContent(
    archive: ComicBookArchive,
    currentPage: Int,
    modifier: Modifier = Modifier,
) {
    BoxWithConstraints(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
        contentAlignment = Alignment.Center,
    ) {
        val density = LocalDensity.current
        val targetWidth = with(density) { maxWidth.roundToPx() }.coerceAtLeast(1)
        val targetHeight = with(density) { maxHeight.roundToPx() }.coerceAtLeast(1)
        var bitmap by remember(archive, currentPage) { mutableStateOf<Bitmap?>(null) }
        var loadError by remember(archive, currentPage) { mutableStateOf<String?>(null) }
        var retryGeneration by remember(archive, currentPage) { mutableIntStateOf(0) }

        LaunchedEffect(archive, currentPage, targetWidth, targetHeight, retryGeneration) {
            bitmap = null
            loadError = null
            try {
                bitmap = withContext(Dispatchers.IO) {
                    archive.decodePage(currentPage, targetWidth, targetHeight)
                }
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Exception) {
                loadError = "Não foi possível abrir esta página do quadrinho."
            }
        }

        when {
            bitmap != null -> Image(
                bitmap = requireNotNull(bitmap).asImageBitmap(),
                contentDescription = "Página ${currentPage + 1} de ${archive.pageCount}",
                contentScale = ContentScale.Fit,
                modifier = Modifier.fillMaxSize(),
            )
            loadError != null -> Row(
                modifier = Modifier.fillMaxWidth().padding(24.dp),
                horizontalArrangement = Arrangement.Center,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(loadError.orEmpty(), textAlign = TextAlign.Center)
                IconButton(onClick = { retryGeneration++ }) {
                    Icon(Icons.Default.Refresh, contentDescription = "Tentar abrir a página novamente")
                }
            }
            else -> CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
        }
    }
}

@Composable
internal fun ComicBookPageControls(
    currentPage: Int,
    pageCount: Int,
    onPageSelected: (Int) -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        IconButton(
            onClick = { onPageSelected(currentPage - 1) },
            enabled = currentPage > 0,
        ) {
            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Página anterior")
        }
        Text(
            text = "Página ${currentPage + 1} de $pageCount",
            style = MaterialTheme.typography.labelMedium,
        )
        IconButton(
            onClick = { onPageSelected(currentPage + 1) },
            enabled = currentPage < pageCount - 1,
        ) {
            Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = "Próxima página")
        }
    }
}
