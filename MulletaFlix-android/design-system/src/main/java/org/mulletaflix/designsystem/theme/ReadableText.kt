package org.mulletaflix.designsystem.theme

import androidx.compose.ui.graphics.Color

/**
 * Lifts semi-transparent text until it is readable on [background].
 *
 * MulletaFlix dims secondary copy with hand-picked alphas (`0.4f`, `0.5f`,
 * `0.6f`, `0.7f`). On the cinematic surfaces some of those composite to
 * measurably unreadable greys. Measured through [contrastRatio]:
 *
 * | colour | alpha | on `DarkSurface` | on `DarkSurfaceContainerHigh` |
 * |---|---|---:|---:|
 * | `White` | 0.4 | 3.83:1 | 3.58:1 |
 * | `onSurface` | 0.4 | 3.21:1 | 3.03:1 |
 * | `onSurface` | 0.5 | 4.29:1 | 3.89:1 |
 *
 * All are below the WCAG 2.2 AA floor of 4.5:1 for normal text, while
 * `White at 0.5` (5.34:1) and everything at `0.6` or above already pass.
 *
 * Instead of replacing each alpha by a new guessed one, this keeps the colour
 * and the *intent* (still dimmer than the primary text) and raises only the
 * alpha until the composited result clears the floor. Combinations that
 * already pass are returned untouched.
 *
 * @param foreground the dimmed colour, usually a colour with `alpha < 1`
 * @param background the surface behind it, expected to be opaque
 * @param minimum required contrast ratio (4.5 for AA normal text, 3.0 for
 *   icons and other non-text UI)
 * @return a colour with the same RGB channels and the smallest alpha that
 *   clears [minimum]
 */
fun readableTextOn(
    foreground: Color,
    background: Color,
    minimum: Double = 4.5,
): Color {
    val baseAlpha = foreground.alpha
    if (contrastRatio(compositeOver(foreground, background), background) >= minimum) return foreground

    // 24 steps of 1/24 keeps the result close to the requested dimming while
    // staying far away from any precision cliff.
    return (1..24)
        .map { step -> baseAlpha + (1f - baseAlpha) * (step / 24f) }
        .firstOrNull { alpha -> contrastRatio(compositeOver(foreground.copy(alpha = alpha), background), background) >= minimum }
        ?.let { alpha -> foreground.copy(alpha = alpha) }
        ?: foreground.copy(alpha = 1f)
}

/**
 * Source-over compositing of [foreground] (possibly translucent) onto an
 * opaque [background], returning the opaque result.
 */
internal fun compositeOver(foreground: Color, background: Color): Color {
    val alpha = foreground.alpha
    return Color(
        red = foreground.red * alpha + background.red * (1f - alpha),
        green = foreground.green * alpha + background.green * (1f - alpha),
        blue = foreground.blue * alpha + background.blue * (1f - alpha),
        alpha = 1f,
    )
}
