package org.mulletaflix.feature.settings

import android.os.Build
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PixelMap
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.toPixelMap
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onRoot
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.filters.SdkSuppress
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.model.SUBTITLE_COLOR_WHITE
import org.mulletaflix.domain.model.SUBTITLE_COLOR_YELLOW

/**
 * The settings preview has to actually reflect the settings.
 *
 * A preview that does not change is worse than none: it claims the choice was
 * understood. These tests measure the rendered pixels rather than reading the
 * code, because a font size or a colour is not something a semantics assertion
 * can see.
 */
@RunWith(AndroidJUnit4::class)
@SdkSuppress(minSdkVersion = Build.VERSION_CODES.O)
class SubtitlePreviewTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun showPreview(sizePercent: Int, colorCode: String) {
        composeRule.setContent {
            MaterialTheme {
                Box(Modifier.fillMaxSize().background(Color.Black)) {
                    SubtitlePreview(sizePercent = sizePercent, colorCode = colorCode)
                }
            }
        }
    }

    private fun capture(): PixelMap = composeRule.onRoot().captureToImage().toPixelMap()

    private fun differingPixels(before: PixelMap, after: PixelMap): Int {
        if (before.width != after.width || before.height != after.height) return Int.MAX_VALUE
        var differing = 0
        for (y in 0 until before.height) {
            for (x in 0 until before.width) {
                if (before[x, y] != after[x, y]) differing++
            }
        }
        return differing
    }

    /**
     * Bounding box of the sample text, measured by looking for the pure white of
     * [SUBTITLE_COLOR_WHITE]'s core stroke.
     *
     * Deliberately not "how many pixels differ from the plate": that counts the
     * plate itself, which grows with the text because the plate has no fixed
     * height. Text drawn larger wraps onto one wide line instead of two, so the
     * plate shrinks while the glyphs grow and the two effects cancel out. The
     * bounding box measures the glyphs alone.
     */
    private fun sampleTextBounds(pixels: PixelMap): IntArray {
        val stroke = Color.White.toArgb()
        var minX = Int.MAX_VALUE
        var maxX = -1
        var minY = Int.MAX_VALUE
        var maxY = -1
        for (y in 0 until pixels.height) {
            for (x in 0 until pixels.width) {
                if (pixels[x, y].toArgb() != stroke) continue
                if (x < minX) minX = x
                if (x > maxX) maxX = x
                if (y < minY) minY = y
                if (y > maxY) maxY = y
            }
        }
        if (maxX < 0) return intArrayOf(0, 0)
        return intArrayOf(maxX - minX + 1, maxY - minY + 1)
    }

    private fun plateBounds(pixels: PixelMap): IntArray {
        val plate = SUBTITLE_PREVIEW_PLATE_COLOR.toArgb()
        var minY = Int.MAX_VALUE
        var maxY = -1
        for (y in 0 until pixels.height) {
            for (x in 0 until pixels.width) {
                if (pixels[x, y].toArgb() != plate) continue
                if (y < minY) minY = y
                if (y > maxY) maxY = y
                break
            }
        }
        if (maxY < 0) return intArrayOf(0, 0)
        return intArrayOf(maxY - minY + 1, 0)
    }

    @Test
    fun aLargerSubtitleSizePaintsMoreText() {
        var size by mutableStateOf(75)
        composeRule.setContent {
            MaterialTheme {
                Box(Modifier.fillMaxSize().background(Color.Black)) {
                    SubtitlePreview(sizePercent = size, colorCode = SUBTITLE_COLOR_WHITE)
                }
            }
        }

        composeRule.waitForIdle()
        val small = sampleTextBounds(capture())
        val plateHeight = plateBounds(capture())[0]

        composeRule.runOnIdle { size = 200 }
        composeRule.waitForIdle()
        val large = sampleTextBounds(capture())

        assertTrue(
            "the sample must be drawn at all; measured ${small[0]}x${small[1]}",
            small[0] > 0 && small[1] > 0,
        )
        assertTrue(
            "200% must render taller glyphs than 75%; measured ${small[1]}px then ${large[1]}px",
            large[1] > small[1],
        )
        assertTrue(
            "200% must also render wider glyphs; measured ${small[0]}px then ${large[0]}px",
            large[0] > small[0],
        )
        assertTrue(
            "the larger sample must stay inside the plate; text ${large[1]}px in a ${plateHeight}px plate",
            large[1] < plateHeight,
        )
    }

    @Test
    fun changingTheColourChangesWhatIsDrawn() {
        var color by mutableStateOf(SUBTITLE_COLOR_WHITE)
        composeRule.setContent {
            MaterialTheme {
                Box(Modifier.fillMaxSize().background(Color.Black)) {
                    SubtitlePreview(sizePercent = 100, colorCode = color)
                }
            }
        }

        composeRule.waitForIdle()
        val white = capture()

        composeRule.runOnIdle { color = SUBTITLE_COLOR_YELLOW }
        composeRule.waitForIdle()
        val yellow = capture()

        assertNotEquals(
            "the preview must repaint when the colour changes",
            0,
            differingPixels(white, yellow),
        )
    }

    @Test
    fun thePreviewRendersTheSameBytesForTheSameSettings() {
        // Guards against a preview that is animated or time-dependent, which would
        // make the two assertions above meaningless.
        showPreview(sizePercent = 150, colorCode = SUBTITLE_COLOR_WHITE)
        composeRule.waitForIdle()

        assertEquals(0, differingPixels(capture(), capture()))
    }
}
