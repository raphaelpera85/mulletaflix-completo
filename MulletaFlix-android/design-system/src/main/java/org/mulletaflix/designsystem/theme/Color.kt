package org.mulletaflix.designsystem.theme

import androidx.compose.ui.graphics.Color

// ────────────────────────────────────────────────────────────────────────────
// MulletaFlix Brand Colors
// Primary: #0F0F0F (preto MulletaFlix)
// Accent: #E50914 (vermelho para detalhes e estados ativos)
// Background: #080808 (preto cinematográfico)
// ────────────────────────────────────────────────────────────────────────────

// Brand
val MulletaFlixBlack = Color(0xFF0F0F0F)

/** Vivid brand red. Only for fills, focus rings and artwork — see [MulletaFlixBrand]. */
val MulletaFlixRed = Color(0xFFE50914)

/**
 * The same brand red lifted until it clears WCAG 2.2 AA (4.5:1) as *text* on
 * the dark surfaces (5.06:1 on [DarkSurface], 5.51:1 on [DarkBackground]).
 * Never use it as a container behind white text (3.64:1).
 */
val MulletaFlixRedAccessible = Color(0xFFFF3333)

val MulletaFlixRedDark = Color(0xFFB20710)
val MulletaFlixRedLight = Color(0xFFFF5A60)

// Dark theme surfaces
val DarkBackground = Color(0xFF080808)
val DarkSurface = Color(0xFF141414)
val DarkSurfaceVariant = Color(0xFF252525)
val DarkSurfaceContainer = Color(0xFF1E1E1E)
val DarkSurfaceContainerHigh = Color(0xFF2A2A2A)
val DarkOnBackground = Color(0xFFE8E8E8)
val DarkOnSurface = Color(0xFFE0E0E0)
val DarkOnSurfaceVariant = Color(0xFF9E9E9E)

/**
 * Boundary colour for inputs (outlined fields) and other component outlines.
 *
 * WCAG 2.2 SC 1.4.11 asks for 3:1 against the adjacent colour, because the
 * border is what identifies the input. The previous #424242 measured only
 * 1.83:1 on [DarkSurface] and 1.43:1 on [DarkSurfaceContainerHigh] — under the
 * 1x non-text minimum of 3:1 an outlined field is effectively invisible.
 * Measured through [contrastRatio], #808080 gives:
 *
 * | surface | ratio |
 * |---|---:|
 * | [DarkSurface] | 4.66:1 |
 * | [DarkBackground] | 5.07:1 |
 * | [DarkSurfaceContainer] | 4.22:1 |
 * | [DarkSurfaceVariant] | 3.88:1 |
 * | [DarkSurfaceContainerHigh] | 3.63:1 |
 */
val DarkOutline = Color(0xFF808080)

/**
 * Hairline dividers between rows. Decorative, so it stays subtle — the
 * component-boundary requirement above is carried by [DarkOutline].
 */
val DarkOutlineVariant = Color(0xFF424242)

// Light theme surfaces
val LightBackground = Color(0xFFF5F5F5)
val LightSurface = Color(0xFFFFFFFF)
val LightSurfaceVariant = Color(0xFFEEEEEE)
val LightOnBackground = Color(0xFF121212)
val LightOnSurface = Color(0xFF1C1C1C)

// Error
val MulletaFlixError = Color(0xFFCF6679)
val MulletaFlixErrorContainer = Color(0xFF4E1B25)
val MulletaFlixOnError = Color(0xFFFFFFFF)
val MulletaFlixOnErrorContainer = Color(0xFFF9DEDC)

// Netflix theme
val NetflixRed = Color(0xFFE50914)
val NetflixRedDark = Color(0xFFB20710)
val NetflixBlack = Color(0xFF141414)
val NetflixSurface = Color(0xFF1F1F1F)

// PurpleHaze theme
val PurpleHazePrimary = Color(0xFF9C27B0)
val PurpleHazePrimaryDark = Color(0xFF7B1FA2)
val PurpleHazeBackground = Color(0xFF120B1A)
val PurpleHazeSurface = Color(0xFF1D1028)

// BlueRadiance theme
val BlueRadiancePrimary = Color(0xFF1565C0)
val BlueRadianceBackground = Color(0xFF0A0F1E)
val BlueRadianceSurface = Color(0xFF0D1628)

// WMC theme — the blue chrome of Windows Media Center, on the cinematic dark base
val WmcBackground = Color(0xFF060A12)
val WmcSurface = Color(0xFF0F1622)
val WmcSurfaceVariant = Color(0xFF1D2836)

// AppleTV theme — tvOS chrome: near-black, slightly cool and a touch lighter
val AppleTvBackground = Color(0xFF0A0A0C)
val AppleTvSurface = Color(0xFF17171A)
val AppleTvSurfaceVariant = Color(0xFF27272C)

// Progress / Badge colors
val ProgressBarColor = MulletaFlixRed
val WatchedBadge = Color(0xFF4CAF50)
val UnwatchedBadge = Color(0xFF9E9E9E)
val HdBadge = Color(0xFF2196F3)
val FourKBadge = Color(0xFFFF9800)
val LiveBadge = Color(0xFFE53935)
val NewBadge = MulletaFlixRed
