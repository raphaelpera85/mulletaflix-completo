package org.mulletaflix.designsystem.components

import org.junit.Assert.assertEquals
import org.junit.Test

class MediaCardPresentationTest {
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
}
