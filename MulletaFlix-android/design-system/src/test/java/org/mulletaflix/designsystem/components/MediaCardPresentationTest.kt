package org.mulletaflix.designsystem.components

import org.junit.Assert.assertEquals
import org.junit.Test
import androidx.compose.ui.layout.ContentScale

class MediaCardPresentationTest {

    @Test
    fun `poster and landscape cards preserve the complete artwork`() {
        assertEquals(ContentScale.Fit, mediaCardContentScale(MediaCardShape.Portrait))
        assertEquals(ContentScale.Fit, mediaCardContentScale(MediaCardShape.Landscape))
    }

    @Test
    fun `square and banner cards keep fill behavior`() {
        assertEquals(ContentScale.Crop, mediaCardContentScale(MediaCardShape.Square))
        assertEquals(ContentScale.Crop, mediaCardContentScale(MediaCardShape.Banner))
    }
    @Test
    fun `progress remains inside compose fraction bounds`() {
        assertEquals(0f, normalizedCardProgress(-0.25f))
        assertEquals(0.45f, normalizedCardProgress(0.45f))
        assertEquals(1f, normalizedCardProgress(1.75f))
    }

    @Test
    fun `non finite progress falls back to zero`() {
        assertEquals(0f, normalizedCardProgress(Float.NaN))
        assertEquals(0f, normalizedCardProgress(Float.POSITIVE_INFINITY))
        assertEquals(0f, normalizedCardProgress(Float.NEGATIVE_INFINITY))
    }

    @Test
    fun `accessibility label exposes visible playback state`() {
        assertEquals(
            "Abrir A Série, na Minha Lista, HD, 3 episódios não assistidos, 42% reproduzido",
            mediaCardAccessibilityLabel(
                title = "A Série",
                isLive = false,
                isWatched = false,
                isFavorite = true,
                qualityBadge = "HD",
                unplayedCount = 3,
                progress = 0.42f,
            ),
        )
    }

    @Test
    fun `watched live cards avoid stale progress announcement`() {
        assertEquals(
            "Abrir Canal 1, ao vivo, assistido",
            mediaCardAccessibilityLabel(
                title = "Canal 1",
                isLive = true,
                isWatched = true,
                isFavorite = false,
                qualityBadge = null,
                unplayedCount = 0,
                progress = 0.9f,
            ),
        )
    }
}
