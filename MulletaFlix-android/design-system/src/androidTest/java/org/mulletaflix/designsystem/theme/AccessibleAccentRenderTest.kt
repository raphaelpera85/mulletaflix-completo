package org.mulletaflix.designsystem.theme

import android.os.Build
import androidx.annotation.RequiresApi
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.unit.dp
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/**
 * Renders the real theme on the device and reads the pixels back.
 *
 * The unit tests prove the contrast maths; this test proves the composable
 * tree actually receives the lifted accent, so a future refactor of
 * [MulletaFlixTheme] that stops calling [withAccessibleAccent] fails here.
 *
 * The assertion is on the measured contrast rather than one exact hex value,
 * because the emulator may composite the swatch through a display colour
 * transform.
 */
class AccessibleAccentRenderTest {

    @get:Rule
    val composeRule = createComposeRule()

    private val swatchTag = "accent-swatch"

    @RequiresApi(Build.VERSION_CODES.O)
    @Test
    fun themeAccentRendersWithAaContrastAgainstTheDarkSurface() {
        composeRule.setContent {
            MulletaFlixTheme(variant = MulletaFlixThemeVariant.Dark) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp)
                        .background(MaterialTheme.colorScheme.secondary)
                        .testTag(swatchTag),
                )
            }
        }

        val rendered = Color(readCenterPixel())
        val ratio = contrastRatio(rendered, DarkSurface)
        assertTrue(
            "rendered accent contrast was ${"%.2f".format(ratio)}:1, expected >= 4.5:1",
            ratio >= 4.5,
        )
        assertNotEquals(
            "the accent must no longer render as the vivid brand red",
            MulletaFlixRed.toArgb(),
            rendered.toArgb(),
        )
    }

    @RequiresApi(Build.VERSION_CODES.O)
    @Test
    fun vividBrandRedStillRendersAsTheContainerColour() {
        composeRule.setContent {
            MulletaFlixTheme(variant = MulletaFlixThemeVariant.Dark) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp)
                        .background(MulletaFlixRed)
                        .testTag(swatchTag),
                )
            }
        }

        val rendered = Color(readCenterPixel())
        assertTrue(
            "a fill must stay recognisably brand red (rendered #${
                "%06X".format(rendered.toArgb() and 0xFFFFFF)
            })",
            contrastRatio(rendered, Color.White) >= 4.5,
        )
        assertNotEquals(
            "the fill must not be the lifted text red",
            MulletaFlixRedAccessible.toArgb(),
            rendered.toArgb(),
        )
    }

    @RequiresApi(Build.VERSION_CODES.O)
    private fun readCenterPixel(): Int {
        val bitmap = composeRule.onNodeWithTag(swatchTag).captureToImage().asAndroidBitmap()
        return bitmap.getPixel(bitmap.width / 2, bitmap.height / 2)
    }
}
