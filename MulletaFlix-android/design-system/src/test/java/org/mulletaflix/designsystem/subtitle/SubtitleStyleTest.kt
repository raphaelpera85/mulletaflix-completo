package org.mulletaflix.designsystem.subtitle

import androidx.compose.ui.graphics.Color
import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.SUBTITLE_COLOR_CYAN
import org.mulletaflix.domain.model.SUBTITLE_COLOR_WHITE
import org.mulletaflix.domain.model.SUBTITLE_COLOR_YELLOW

/**
 * Only the Compose half: turning a stored code into a `Color`.
 *
 * Codes, the accepted set, the size range and the size maths are covered by
 * `SubtitleStylePolicyTest` in `:domain`, which is where they live.
 */
class SubtitleStyleTest {

    @Test
    fun `maps colours to foreground values`() {
        assertEquals(Color.White, subtitleForegroundColor(SUBTITLE_COLOR_WHITE))
        assertEquals(Color.Yellow, subtitleForegroundColor(SUBTITLE_COLOR_YELLOW))
        assertEquals(Color.Cyan, subtitleForegroundColor(SUBTITLE_COLOR_CYAN))
    }

    @Test
    fun `an unknown code draws the default rather than failing`() {
        assertEquals(Color.White, subtitleForegroundColor("magenta"))
        assertEquals(Color.White, subtitleForegroundColor(null))
    }
}
