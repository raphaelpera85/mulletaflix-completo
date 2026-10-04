package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test

class ComicPageZoomTest {
    @Test
    fun zoomIsLimitedToOneHundredThroughFourHundredPercent() {
        assertEquals(1f, normalizeComicPageZoom(0.5f))
        assertEquals(1f, normalizeComicPageZoom(1f))
        assertEquals(2.5f, normalizeComicPageZoom(2.5f))
        assertEquals(MAX_COMIC_PAGE_ZOOM, normalizeComicPageZoom(5f))
    }

    @Test
    fun invalidZoomValuesResetToFitPage() {
        assertEquals(1f, normalizeComicPageZoom(Float.NaN))
        assertEquals(1f, normalizeComicPageZoom(Float.POSITIVE_INFINITY))
        assertEquals(1f, normalizeComicPageZoom(Float.NEGATIVE_INFINITY))
    }

    @Test
    fun panBoundsFollowVisiblePageAndKeepLetterboxedAreasOutOfView() {
        val bounds = comicPagePanBounds(
            viewportWidth = 1_000f,
            viewportHeight = 1_000f,
            imageWidth = 2_000f,
            imageHeight = 3_000f,
            zoom = 2f,
        )

        assertEquals(166.66667f, bounds.x, 0.01f)
        assertEquals(500f, bounds.y, 0.01f)
    }

    @Test
    fun panBoundsCollapseToZeroWhenImageFitsTheViewport() {
        val bounds = comicPagePanBounds(
            viewportWidth = 1_000f,
            viewportHeight = 1_000f,
            imageWidth = 2_000f,
            imageHeight = 3_000f,
            zoom = 1f,
        )

        assertEquals(0f, bounds.x)
        assertEquals(0f, bounds.y)
    }
}
