package org.mulletaflix.feature.player

import android.graphics.Color
import org.junit.Assert.assertEquals
import org.junit.Test

class SubtitleStylePolicyTest {

    @Test
    fun `normalizes supported subtitle colors and safe fallback`() {
        assertEquals(SUBTITLE_COLOR_WHITE, normalizeSubtitleColor(null))
        assertEquals(SUBTITLE_COLOR_YELLOW, normalizeSubtitleColor("yellow"))
        assertEquals(SUBTITLE_COLOR_CYAN, normalizeSubtitleColor(" CYAN "))
        assertEquals(SUBTITLE_COLOR_WHITE, normalizeSubtitleColor("magenta"))
    }

    @Test
    fun `maps subtitle colors to platform foreground values`() {
        assertEquals(Color.WHITE, subtitleForegroundColor(SUBTITLE_COLOR_WHITE))
        assertEquals(Color.YELLOW, subtitleForegroundColor(SUBTITLE_COLOR_YELLOW))
        assertEquals(Color.CYAN, subtitleForegroundColor(SUBTITLE_COLOR_CYAN))
    }
}
