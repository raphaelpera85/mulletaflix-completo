package org.mulletaflix.feature.itemdetail

import androidx.compose.ui.geometry.Offset

/** Maximum render resolution for a page, bounded by the archive decoder's pixel budget. */
internal const val MAX_COMIC_PAGE_ZOOM = 4f

internal fun normalizeComicPageZoom(value: Float): Float =
    if (value.isFinite()) value.coerceIn(1f, MAX_COMIC_PAGE_ZOOM) else 1f

/** Returns the maximum pan distance without exposing letterboxed space around the page. */
internal fun comicPagePanBounds(
    viewportWidth: Float,
    viewportHeight: Float,
    imageWidth: Float,
    imageHeight: Float,
    zoom: Float,
): Offset {
    if (viewportWidth <= 0f || viewportHeight <= 0f || imageWidth <= 0f || imageHeight <= 0f) {
        return Offset.Zero
    }
    val fitScale = minOf(viewportWidth / imageWidth, viewportHeight / imageHeight)
    val scale = normalizeComicPageZoom(zoom)
    return Offset(
        x = ((imageWidth * fitScale * scale) - viewportWidth).coerceAtLeast(0f) / 2f,
        y = ((imageHeight * fitScale * scale) - viewportHeight).coerceAtLeast(0f) / 2f,
    )
}
