package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * One definition of what a subtitle preference is.
 *
 * The colour codes and the size clamp used to exist twice — in `:design-system` and
 * again in `SettingsRepositoryImpl` — and the settings preview is drawn from this
 * mapping, so a drift would make the preview promise something the player does not do.
 */
class SubtitleStylePolicyTest {

    @Test
    fun `normalizes supported colours and falls back safely`() {
        assertEquals(SUBTITLE_COLOR_WHITE, normalizeSubtitleColor(null))
        assertEquals(SUBTITLE_COLOR_YELLOW, normalizeSubtitleColor("yellow"))
        assertEquals(SUBTITLE_COLOR_CYAN, normalizeSubtitleColor(" CYAN "))
        assertEquals(SUBTITLE_COLOR_WHITE, normalizeSubtitleColor("magenta"))
    }

    @Test
    fun `every offered colour normalizes to itself`() {
        // Guards the option list against a code the mapping does not know: an unknown
        // code would store one thing and draw another.
        subtitleColorCodes.forEach { code ->
            assertEquals(
                "the menu offers \"$code\", so it must normalize to itself",
                code,
                normalizeSubtitleColor(code),
            )
            assertTrue(subtitleColorCodes.contains(normalizeSubtitleColor(code)))
        }
    }

    @Test
    fun `size is clamped to the range the app supports`() {
        assertEquals(MIN_SUBTITLE_SIZE_PERCENT, normalizeSubtitleSizePercent(10))
        assertEquals(100, normalizeSubtitleSizePercent(100))
        assertEquals(MAX_SUBTITLE_SIZE_PERCENT, normalizeSubtitleSizePercent(500))
    }

    @Test
    fun `the fractional size grows with the stored percentage`() {
        val sizes = listOf(50, 100, 200).map(::subtitleFractionalTextSize)

        assertTrue("50% must be the smallest", sizes[0] < sizes[1])
        assertTrue("200% must be the largest", sizes[1] < sizes[2])
        assertEquals(
            "the multiplier is what makes 150% mean the same in the player and the preview",
            subtitleFractionalTextSize(100) * 1.5f,
            subtitleFractionalTextSize(150),
            0.0001f,
        )
    }

    @Test
    fun `an out of range size is clamped the same way for drawing`() {
        assertEquals(
            subtitleFractionalTextSize(MAX_SUBTITLE_SIZE_PERCENT),
            subtitleFractionalTextSize(999),
        )
    }
}
