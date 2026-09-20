package org.mulletaflix.designsystem.components

import androidx.compose.ui.graphics.Color
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MulletaFlixWordmarkTest {
    @Test
    fun `wordmark keeps MULLETA red and FLIX white`() {
        val wordmark = buildMulletaFlixWordmark(red = Color.Red, white = Color.White)

        assertEquals("MULLETAFLIX", wordmark.text)
        assertTrue(wordmark.spanStyles.any { it.start == 0 && it.end == 7 && it.item.color == Color.Red })
        assertTrue(wordmark.spanStyles.any { it.start == 7 && it.end == 11 && it.item.color == Color.White })
    }
}
