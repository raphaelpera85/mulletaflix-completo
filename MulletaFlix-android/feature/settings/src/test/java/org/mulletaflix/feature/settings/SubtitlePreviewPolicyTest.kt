package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MAX_SUBTITLE_SIZE_PERCENT
import org.mulletaflix.domain.model.subtitleFractionalTextSize

/**
 * The preview must show the same progression the player will produce.
 *
 * It is derived from the shared fractional size rather than from a second
 * formula, so "150%" cannot mean one thing in the settings screen and another in
 * the player.
 */
class SubtitlePreviewPolicyTest {

    @Test
    fun `the preview text grows with the stored percentage`() {
        val sizes = subtitleFontSizeChoices.map(::subtitlePreviewFontSizeSp)

        assertEquals("the offered sizes ascend, so must the preview", sizes.sorted(), sizes)
        assertTrue("the smallest must be legible", sizes.first() >= 8f)
        assertTrue("the largest must fit the plate", sizes.last() <= 64f)
    }

    @Test
    fun `the preview uses the shared fractional size`() {
        val expected = SUBTITLE_PREVIEW_REFERENCE_HEIGHT_DP * subtitleFractionalTextSize(100)

        assertEquals(expected, subtitlePreviewFontSizeSp(100), 0.0001f)
    }

    @Test
    fun `200 percent is twice 100 percent in the preview too`() {
        assertEquals(
            subtitlePreviewFontSizeSp(100) * 2f,
            subtitlePreviewFontSizeSp(MAX_SUBTITLE_SIZE_PERCENT),
            0.0001f,
        )
    }

    @Test
    fun `an out of range size cannot overflow the plate`() {
        assertEquals(
            subtitlePreviewFontSizeSp(MAX_SUBTITLE_SIZE_PERCENT),
            subtitlePreviewFontSizeSp(9999),
        )
    }
}
