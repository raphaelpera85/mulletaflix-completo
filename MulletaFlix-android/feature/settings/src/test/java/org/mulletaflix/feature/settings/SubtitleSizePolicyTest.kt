package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Test

class SubtitleSizePolicyTest {
    @Test
    fun `clamps subtitle size to accessible range`() {
        assertEquals(50, normalizeSubtitleFontSize(10))
        assertEquals(200, normalizeSubtitleFontSize(500))
        assertEquals(125, normalizeSubtitleFontSize(125))
    }
}
