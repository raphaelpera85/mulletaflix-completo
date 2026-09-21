package org.mulletaflix.designsystem.theme

import androidx.compose.material3.ColorScheme
import androidx.compose.ui.graphics.Color
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Guards the brand colour policy with the real WCAG 2.2 contrast maths.
 *
 * These assertions are the reason every theme's accent is lifted before it
 * reaches MaterialTheme: the existing vivid reds measured 3.84:1 (dark),
 * 3.44:1 (netflix), 2.88:1 (purple haze) and 3.14:1 (blue radiance) as text,
 * all below the 4.5:1 AA floor for normal text.
 */
class BrandColorContrastTest {

    private val aaNormalText = 4.5
    private val aaLargeTextAndUi = 3.0

    private fun assertContrast(
        label: String,
        foreground: Color,
        background: Color,
        minimum: Double,
    ) {
        val ratio = contrastRatio(foreground, background)
        assertTrue(
            "$label contrast was ${"%.2f".format(ratio)}:1, expected >= $minimum:1",
            ratio >= minimum,
        )
    }

    private fun assertAccentReadable(label: String, scheme: ColorScheme) {
        assertContrast("$label accent on surface", scheme.secondary, scheme.surface, aaNormalText)
        assertContrast("$label accent on background", scheme.secondary, scheme.background, aaNormalText)
        assertContrast("$label accent on surfaceVariant", scheme.secondary, scheme.surfaceVariant, aaNormalText)
    }

    /** Mirrors the themes exposed by [MulletaFlixTheme]. */
    private val themeSchemes: List<Pair<String, ColorScheme>> = listOf(
        "Dark" to DarkColorScheme,
        "Light" to LightColorScheme,
        "Netflix" to NetflixColorScheme,
        "PurpleHaze" to PurpleHazeColorScheme,
        "BlueRadiance" to BlueRadianceColorScheme,
    )

    @Test
    fun `every theme accent is readable on its own surfaces after lifting`() {
        themeSchemes.forEach { (name, scheme) ->
            assertAccentReadable(name, scheme.withAccessibleAccent())
        }
    }

    @Test
    fun `vivid brand red was not readable as text before lifting`() {
        // Documents the defect this policy fixes; the assertion is inverted on
        // purpose so that a future brand change re-opens the calibration.
        assertTrue(
            "vivid red now passes as text — the lifted red and its tests can be revisited",
            contrastRatio(MulletaFlixRed, DarkSurface) < aaNormalText,
        )
        // The same vivid red must stay readable as a container behind white.
        assertContrast("white on vivid red", Color.White, MulletaFlixRed, aaNormalText)
        // And it must stay visible as a focus ring / progress indicator.
        assertContrast("vivid red on DarkBackground", MulletaFlixRed, DarkBackground, aaLargeTextAndUi)
    }

    @Test
    fun `dark scheme accent is the calibrated accessible red for its worst case surface`() {
        val lifted = DarkColorScheme.withAccessibleAccent()
        assertEquals(
            "the accent must be exactly the calibration of the worst-case surface",
            accessibleAccent(DarkColorScheme.secondary, DarkSurfaceVariant),
            lifted.secondary,
        )
        assertAccentReadable("Dark", lifted)
    }

    @Test
    fun `worst case surface is the lightest on dark themes and the darkest on light themes`() {
        val darkWorst = worstCaseAccentSurface(DarkSurface, DarkBackground, DarkSurfaceVariant)
        assertEquals(DarkSurfaceVariant, darkWorst)
        listOf(DarkSurface, DarkBackground).forEach {
            assertTrue(
                "the dark worst case must not be lighter than another dark surface",
                relativeLuminance(darkWorst) >= relativeLuminance(it),
            )
        }

        val lightWorst = worstCaseAccentSurface(LightSurface, LightBackground, LightSurfaceVariant)
        listOf(LightSurface, LightBackground, LightSurfaceVariant).forEach {
            assertTrue(
                "the light worst case must not be darker than another light surface",
                relativeLuminance(lightWorst) <= relativeLuminance(it),
            )
        }
        // Whatever surface the light theme is calibrated against, the accent
        // it produces must clear the floor on all three.
        val lightAccent = accessibleAccent(LightColorScheme.secondary, lightWorst)
        listOf(LightSurface, LightBackground, LightSurfaceVariant).forEach {
            assertContrast("light accent on $it", lightAccent, it, aaNormalText)
        }
    }

    @Test
    fun `lifting is monotonic towards the background direction`() {
        val lifted = accessibleAccent(MulletaFlixRed, NetflixSurface)
        assertTrue(
            "a dark theme accent must be lifted towards white",
            relativeLuminance(lifted) > relativeLuminance(MulletaFlixRed),
        )
        assertContrast("lifted netflix accent", lifted, NetflixSurface, aaNormalText)

        val lightLifted = accessibleAccent(Color(0xFFFF8A80), LightSurface)
        assertTrue(
            "a light theme accent must be darkened",
            relativeLuminance(lightLifted) < relativeLuminance(Color(0xFFFF8A80)),
        )
        assertContrast("darkened light accent", lightLifted, LightSurface, aaNormalText)
    }

    @Test
    fun `an accent that already clears the floor is left untouched`() {
        assertEquals(
            MulletaFlixRedAccessible,
            accessibleAccent(MulletaFlixRedAccessible, DarkSurface),
        )
    }

    @Test
    fun `filled accent containers pick a readable content colour`() {
        assertEquals(Color.White, readableOn(MulletaFlixRed))
        assertEquals(Color.Black, readableOn(Color(0xFFFFEE00)))
    }

    @Test
    fun `the hand picked dim alphas are measurably unreadable`() {
        // Documents why readableOnBackground exists. These are the exact pairs
        // the app used before this policy.
        val combinations = listOf(
            Color.White.copy(alpha = 0.4f) to DarkSurface,
            Color.White.copy(alpha = 0.5f) to DarkSurface,
            DarkOnSurface.copy(alpha = 0.4f) to DarkSurface,
            DarkOnSurface.copy(alpha = 0.5f) to DarkSurface,
            Color.White.copy(alpha = 0.5f) to DarkBackground,
        )
        combinations.forEach { (foreground, background) ->
            val composited = compositeOver(foreground, background)
            val ratio = contrastRatio(composited, background)
            assertTrue(
                "expected the raw alpha to fail AA (was ${"%.2f".format(ratio)}:1)",
                ratio < aaNormalText,
            )
            assertContrast(
                "lifted $foreground",
                compositeOver(readableOnBackground(foreground, background), background),
                background,
                aaNormalText,
            )
        }
    }

    @Test
    fun `lifting keeps the colour identity and only raises the alpha`() {
        val lifted = readableOnBackground(Color.White.copy(alpha = 0.4f), DarkSurface)
        assertEquals(Color.White, lifted.copy(alpha = 1f))
        assertTrue(
            "the lifted alpha must stay below fully opaque (was ${lifted.alpha})",
            lifted.alpha < 1f,
        )
        assertTrue(
            "the lifted alpha must be above the original",
            lifted.alpha > 0.4f,
        )
    }

    @Test
    fun `an already readable dim colour is returned untouched`() {
        val alreadyReadable = Color.White.copy(alpha = 0.9f)
        assertEquals(alreadyReadable, readableOnBackground(alreadyReadable, DarkSurface))
    }

    @Test
    fun `non text ui only needs the three to one floor`() {
        val icon = readableOnBackground(
            foreground = DarkOnSurface.copy(alpha = 0.4f),
            background = DarkSurface,
            minimum = aaLargeTextAndUi,
        )
        assertContrast(
            "lifted icon",
            compositeOver(icon, DarkSurface),
            DarkSurface,
            aaLargeTextAndUi,
        )
        assertTrue(
            "a 3:1 icon must not be lifted as far as 4.5:1 text",
            icon.alpha <= readableOnBackground(DarkOnSurface.copy(alpha = 0.4f), DarkSurface).alpha,
        )
    }

    @Test
    fun `light scheme keeps the brand red readable as a filled container`() {
        assertContrast(
            "light onPrimary on light primary",
            LightColorScheme.onPrimary,
            LightColorScheme.primary,
            aaNormalText,
        )
    }
}
