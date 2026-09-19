package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ShareItemContentTest {

    @Test
    fun `builds encoded detail link and removes trailing slash`() {
        assertEquals(
            "http://server:8096/web/index.html#!/details?id=movie%2F1+edition",
            buildItemShareUrl(" http://server:8096/// ", " movie/1 edition ")
        )
    }

    @Test
    fun `share text includes title and server link`() {
        val text = buildItemShareText(" O Filme ", "movie-1", "http://server:8096")

        assertTrue(text.contains("\"O Filme\""))
        assertTrue(text.contains("http://server:8096/web/index.html#!/details?id=movie-1"))
    }

    @Test
    fun `blank server does not create a broken link`() {
        assertNull(buildItemShareUrl("  ", "movie-1"))
        assertEquals("Confira \"O Filme\" no MulletaFlix.", buildItemShareText("O Filme", "movie-1", ""))
    }
}
