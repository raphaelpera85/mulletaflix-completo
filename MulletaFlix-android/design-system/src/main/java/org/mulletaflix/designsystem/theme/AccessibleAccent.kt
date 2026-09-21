package org.mulletaflix.designsystem.theme

import androidx.compose.material3.ColorScheme
import androidx.compose.ui.graphics.Color

/**
 * MulletaFlix brand colour policy.
 *
 * The vivid brand red is readable as a **fill** — it is dark enough for white
 * text on top of it (4.79:1) — but it is not readable as **text**: measured
 * against the cinematic dark surfaces it reaches only 4.18:1 on the
 * background and 3.84:1 on the surface, below the WCAG 2.2 AA minimum of
 * 4.5:1 for normal text. Netflix, Purple Haze and Blue Radiance were worse.
 *
 * So the red is split in two roles:
 *
 * - [MulletaFlixRed] — containers, progress fills, focus rings, artwork.
 * - the theme accent (`colorScheme.secondary`) — labels, icons and outlines.
 *
 * `MulletaFlixTheme` lifts every theme's accent through [accessibleAccent]
 * before handing it to MaterialTheme, so new code that asks the theme for the
 * accent colour gets a readable red by default.
 */

/**
 * Relative luminance of a colour as defined by WCAG 2.2 (1.4.3).
 *
 * Kept dependency-free so both production code and unit tests share the exact
 * same measurement instead of duplicating the formula.
 */
internal fun relativeLuminance(color: Color): Double {
    fun channel(value: Float): Double {
        val c = value.toDouble()
        return if (c <= 0.03928) c / 12.92 else Math.pow((c + 0.055) / 1.055, 2.4)
    }
    return 0.2126 * channel(color.red) +
        0.7152 * channel(color.green) +
        0.0722 * channel(color.blue)
}

/** WCAG contrast ratio between two opaque colours, from 1.0 to 21.0. */
internal fun contrastRatio(foreground: Color, background: Color): Double {
    val first = relativeLuminance(foreground)
    val second = relativeLuminance(background)
    val lighter = maxOf(first, second)
    val darker = minOf(first, second)
    return (lighter + 0.05) / (darker + 0.05)
}

/**
 * Lifts a brand accent until it clears the WCAG 2.2 AA floor (4.5:1) as text
 * on the given background.
 *
 * MulletaFlix keeps one visual red for artwork, fills and focus rings, but
 * the same red is too dark to be *text* on the cinematic surfaces. Instead of
 * hand-tuning a second red per theme, every theme derives its readable accent
 * from its own brand red and its own background, so a new theme cannot
 * silently ship unreadable accent text.
 *
 * @param vivid the theme's brand red
 * @param background the surface the accent is drawn on
 * @param minimum required contrast ratio (4.5 for AA normal text)
 */
internal fun accessibleAccent(
    vivid: Color,
    background: Color,
    minimum: Double = 4.5,
): Color {
    if (contrastRatio(vivid, background) >= minimum) return vivid

    val liftTowardsWhite = relativeLuminance(background) < 0.18
    return generateSequence(1) { it + 1 }
        .map { step ->
            val amount = step / 100f
            if (liftTowardsWhite) {
                Color(
                    red = vivid.red + (1f - vivid.red) * amount,
                    green = vivid.green + (1f - vivid.green) * amount,
                    blue = vivid.blue + (1f - vivid.blue) * amount,
                    alpha = vivid.alpha,
                )
            } else {
                Color(
                    red = vivid.red * (1f - amount),
                    green = vivid.green * (1f - amount),
                    blue = vivid.blue * (1f - amount),
                    alpha = vivid.alpha,
                )
            }
        }
        .first { contrastRatio(it, background) >= minimum }
}

/**
 * Returns the scheme with its accent roles replaced by [accessibleAccent].
 *
 * The lift is calibrated against the surfaces the accent is actually drawn
 * on — `surface`, `background` and `surfaceVariant` — choosing the worst case
 * so one readable red covers all three. Only the roles that carry text or
 * icons are rewritten; fills such as `primaryContainer` keep the vivid brand
 * red.
 */
internal fun ColorScheme.withAccessibleAccent(): ColorScheme {
    val worstCaseSurface = worstCaseAccentSurface(
        surface = surface,
        background = background,
        surfaceVariant = surfaceVariant,
    )
    val readableSecondary = accessibleAccent(secondary, worstCaseSurface)
    val readableTertiary = accessibleAccent(tertiary, worstCaseSurface)
    return copy(
        secondary = readableSecondary,
        tertiary = readableTertiary,
        onSecondary = readableOn(readableSecondary),
        onTertiary = readableOn(readableTertiary),
    )
}

/**
 * Picks the surface that makes accent text hardest to read.
 *
 * On a dark theme that is the *lightest* surface; on a light theme it is the
 * *darkest* one. Calibrating against this surface guarantees the accent also
 * clears the floor on the other two.
 */
internal fun worstCaseAccentSurface(
    surface: Color,
    background: Color,
    surfaceVariant: Color,
): Color {
    val candidates = listOf(surface, background, surfaceVariant)
    val darkTheme = relativeLuminance(surface) < 0.18
    return if (darkTheme) {
        candidates.maxBy { relativeLuminance(it) }
    } else {
        candidates.minBy { relativeLuminance(it) }
    }
}

/** Picks the readable content colour for a filled accent container. */
internal fun readableOn(container: Color): Color =
    if (contrastRatio(Color.White, container) >= contrastRatio(Color.Black, container)) {
        Color.White
    } else {
        Color.Black
    }
