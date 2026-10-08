package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.foundation.ExperimentalFoundationApi
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
import androidx.compose.material.icons.filled.ZoomIn
import androidx.compose.material.icons.filled.ZoomOut
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.geometry.Offset
import androidx.compose.foundation.gestures.rememberTransformableState
import androidx.compose.foundation.gestures.transformable
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import kotlin.math.roundToInt
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.withContext

@Composable
@OptIn(ExperimentalFoundationApi::class)
internal fun PagedBookReaderContent(
    pageBook: BookPageSource,
    currentPage: Int,
    zoom: Float = 1f,
    onZoomChange: (Float) -> Unit = {},
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
        var bitmap by remember(pageBook, currentPage) { mutableStateOf<Bitmap?>(null) }
        DisposableEffect(bitmap) {
            val displayedBitmap = bitmap
            onDispose {
                if (displayedBitmap != null && !displayedBitmap.isRecycled) displayedBitmap.recycle()
            }
        }
        var decodedZoomTier by remember(pageBook, currentPage) { mutableIntStateOf(0) }
        var loadError by remember(pageBook, currentPage) { mutableStateOf<String?>(null) }
        var retryGeneration by remember(pageBook, currentPage) { mutableIntStateOf(0) }
        var panOffset by remember(pageBook, currentPage) { mutableStateOf(Offset.Zero) }
        val transformState = rememberTransformableState { centroid, zoomChange, panChange, _ ->
            val currentZoom = normalizeComicPageZoom(zoom)
            val nextZoom = normalizeComicPageZoom(currentZoom * zoomChange)
            val effectiveZoomChange = nextZoom / currentZoom
            onZoomChange(nextZoom)
            val viewportWidth = with(density) { maxWidth.toPx() }
            val viewportHeight = with(density) { maxHeight.toPx() }
            val imageBounds = bitmap?.let {
                comicPagePanBounds(viewportWidth, viewportHeight, it.width.toFloat(), it.height.toFloat(), nextZoom)
            } ?: Offset.Zero
            val centroidFromCenter = Offset(
                x = centroid.x - viewportWidth / 2f,
                y = centroid.y - viewportHeight / 2f,
            )
            val zoomOffset = centroidFromCenter - (centroidFromCenter - panOffset) * effectiveZoomChange
            panOffset = constrainComicPagePan(zoomOffset + panChange, imageBounds)
            if (nextZoom == 1f) panOffset = Offset.Zero
        }
        LaunchedEffect(zoom) {
            val currentBitmap = bitmap ?: return@LaunchedEffect
            val imageBounds = comicPagePanBounds(
                viewportWidth = with(density) { maxWidth.toPx() },
                viewportHeight = with(density) { maxHeight.toPx() },
                imageWidth = currentBitmap.width.toFloat(),
                imageHeight = currentBitmap.height.toFloat(),
                zoom = zoom,
            )
            panOffset = constrainComicPagePan(panOffset, imageBounds)
        }

        LaunchedEffect(pageBook, currentPage, targetWidth, targetHeight, retryGeneration) {
            bitmap = null
            decodedZoomTier = 0
            loadError = null
            try {
                bitmap = withContext(Dispatchers.IO) {
                    val decoded = pageBook.decodePage(currentPage, targetWidth, targetHeight)
                    try {
                        currentCoroutineContext().ensureActive()
                        decoded
                    } catch (cancelled: CancellationException) {
                        decoded.recycle()
                        throw cancelled
                    }
                }
                decodedZoomTier = 1
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Exception) {
                loadError = "Não foi possível abrir esta página do quadrinho."
            }
        }

        LaunchedEffect(pageBook, currentPage, targetWidth, targetHeight, zoom, bitmap, decodedZoomTier) {
            if (bitmap == null) return@LaunchedEffect
            val targetTier = comicPageRenderTier(zoom)
            if (targetTier == decodedZoomTier) return@LaunchedEffect
            delay(COMIC_PAGE_RENDER_DEBOUNCE_MS)
            try {
                val replacement = withContext(Dispatchers.IO) {
                    val decoded = pageBook.decodePage(
                        currentPage,
                        maxWidth = (targetWidth.toLong() * targetTier)
                            .coerceAtMost(Int.MAX_VALUE.toLong()).toInt(),
                        maxHeight = (targetHeight.toLong() * targetTier)
                            .coerceAtMost(Int.MAX_VALUE.toLong()).toInt(),
                    )
                    try {
                        currentCoroutineContext().ensureActive()
                        decoded
                    } catch (cancelled: CancellationException) {
                        decoded.recycle()
                        throw cancelled
                    }
                }
                bitmap = replacement
                decodedZoomTier = targetTier
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Exception) {
                // Keep last successful bitmap if the higher-resolution decode fails.
            }
        }

        when {
            bitmap != null -> Box(
                modifier = Modifier.fillMaxSize().clipToBounds(),
                contentAlignment = Alignment.Center,
            ) {
                Image(
                    bitmap = requireNotNull(bitmap).asImageBitmap(),
                    contentDescription = "Página ${currentPage + 1} de ${pageBook.pageCount}, ampliação ${(zoom * 100).roundToInt()}%",
                    contentScale = ContentScale.Fit,
                    modifier = Modifier
                        .fillMaxSize()
                        .testTag("comic-book-page")
                        .graphicsLayer {
                            scaleX = zoom
                            scaleY = zoom
                            translationX = panOffset.x
                            translationY = panOffset.y
                        }
                        .transformable(transformState),
                )
            }
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
internal fun ComicBookZoomControls(
    zoom: Float,
    onZoomChange: (Float) -> Unit,
) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        IconButton(
            onClick = { onZoomChange(normalizeComicPageZoom(zoom - COMIC_PAGE_ZOOM_STEP)) },
            enabled = zoom > 1f,
            modifier = Modifier.semantics(mergeDescendants = true) {
                contentDescription = "Reduzir ampliação"
            },
        ) {
            Icon(Icons.Default.ZoomOut, contentDescription = null)
        }
        Text("${(zoom * 100).roundToInt()}%", style = MaterialTheme.typography.labelMedium)
        IconButton(
            onClick = { onZoomChange(normalizeComicPageZoom(zoom + COMIC_PAGE_ZOOM_STEP)) },
            enabled = zoom < MAX_COMIC_PAGE_ZOOM,
            modifier = Modifier.semantics(mergeDescendants = true) {
                contentDescription = "Ampliar página"
            },
        ) {
            Icon(Icons.Default.ZoomIn, contentDescription = null)
        }
    }
}

private const val COMIC_PAGE_ZOOM_STEP = 0.5f
private const val COMIC_PAGE_RENDER_DEBOUNCE_MS = 250L

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
