package org.mulletaflix.designsystem.theme

import androidx.compose.ui.graphics.Color
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Diagnostic helper used while calibrating [readableOnBackground].
 *
 * It asserts nothing about specific alphas; it records, as a failing message,
 * the measured contrast of each dim-text combination so the numbers come from
 * the same implementation the app uses instead of from hand arithmetic.
 */
class DimTextContrastCalibrationTest {

    private val surfaces = listOf(
        "DarkSurface" to DarkSurface,
        "DarkBackground" to DarkBackground,
        "DarkSurfaceContainer" to DarkSurfaceContainer,
        "DarkSurfaceVariant" to DarkSurfaceVariant,
        "DarkSurfaceContainerHigh" to DarkSurfaceContainerHigh,
    )

    @Test
    fun recordMeasuredContrastForDimText() {
        val report = StringBuilder("\n")
        surfaces.forEach { (surfaceName, surface) ->
            report.append("== $surfaceName ==\n")
            listOf(0.4f, 0.5f, 0.6f, 0.7f, 0.8f).forEach { alpha ->
                val white = compositeOver(Color.White.copy(alpha = alpha), surface)
                val onSurface = compositeOver(DarkOnSurface.copy(alpha = alpha), surface)
                report.append(
                    "  alpha=%.1f white=%.2f onSurface=%.2f liftedWhite=%s liftedOnSurface=%s\n".format(
                        alpha,
                        contrastRatio(white, surface),
                        contrastRatio(onSurface, surface),
                        "%.2f".format(
                            contrastRatio(
                                compositeOver(
                                    readableOnBackground(Color.White.copy(alpha = alpha), surface),
                                    surface,
                                ),
                                surface,
                            ),
                        ),
                        "%.2f".format(
                            contrastRatio(
                                compositeOver(
                                    readableOnBackground(DarkOnSurface.copy(alpha = alpha), surface),
                                    surface,
                                ),
                                surface,
                            ),
                        ),
                    ),
                )
            }
            listOf(
                "outline" to DarkOutline,
                "outlineVariant" to DarkOutlineVariant,
            ).forEach { (name, color) ->
                report.append("  $name=%.2f\n".format(contrastRatio(color, surface)))
            }
        }
        assertTrue(report.toString(), false)
    }
}
