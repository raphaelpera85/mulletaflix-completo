package org.mulletaflix.designsystem.theme

import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.ui.graphics.Color

/** Default dark theme — preto cinematográfico com vermelho MulletaFlix */
val DarkColorScheme = darkColorScheme(
    // `primary` stays the cinematic black used by neutral containers.
    primary = MulletaFlixBlack,
    onPrimary = Color.White,
    primaryContainer = MulletaFlixRedDark,
    onPrimaryContainer = Color.White,
    // The theme accent is the accessible red so every label, icon and outline
    // that reads the accent colour from the theme clears WCAG AA on dark.
    // Fills that need white text on top keep [MulletaFlixRed] explicitly.
    secondary = MulletaFlixRedAccessible,
    onSecondary = Color.White,
    secondaryContainer = MulletaFlixRedDark,
    onSecondaryContainer = Color.White,
    background = DarkBackground,
    onBackground = DarkOnBackground,
    surface = DarkSurface,
    onSurface = DarkOnSurface,
    surfaceVariant = DarkSurfaceVariant,
    onSurfaceVariant = DarkOnSurfaceVariant,
    outline = DarkOutline,
    outlineVariant = DarkOutlineVariant,
    error = MulletaFlixError,
    onError = MulletaFlixOnError,
    errorContainer = MulletaFlixErrorContainer,
    onErrorContainer = MulletaFlixOnErrorContainer,
)

/**
 * Light theme.
 *
 * `primary` is the darker brand red on purpose: a white label on the vivid
 * #E50914 only reaches 4.40:1 (4.79:1 is measured against the dark surface
 * where the same red is used as a fill), so the light theme uses the darker
 * brand red to clear AA on white.
 */
val LightColorScheme = lightColorScheme(
    primary = MulletaFlixRedDark,
    onPrimary = LightBackground,
    primaryContainer = MulletaFlixRedLight,
    onPrimaryContainer = MulletaFlixRedDark,
    background = LightBackground,
    onBackground = LightOnBackground,
    surface = LightSurface,
    onSurface = LightOnSurface,
    surfaceVariant = LightSurfaceVariant,
    onSurfaceVariant = LightOnBackground,
    error = MulletaFlixError,
    onError = MulletaFlixOnError,
)

/** Netflix theme — black background, red accent */
val NetflixColorScheme = darkColorScheme(
    primary = NetflixRed,
    onPrimary = LightBackground,
    primaryContainer = NetflixRedDark,
    onPrimaryContainer = LightBackground,
    background = NetflixBlack,
    onBackground = DarkOnBackground,
    surface = NetflixSurface,
    onSurface = DarkOnSurface,
    surfaceVariant = Color(0xFF292929),
    error = MulletaFlixError,
    onError = MulletaFlixOnError,
)

/** PurpleHaze theme */
val PurpleHazeColorScheme = darkColorScheme(
    primary = PurpleHazePrimary,
    onPrimary = LightBackground,
    primaryContainer = PurpleHazePrimaryDark,
    background = PurpleHazeBackground,
    onBackground = DarkOnBackground,
    surface = PurpleHazeSurface,
    onSurface = DarkOnSurface,
    error = MulletaFlixError,
    onError = MulletaFlixOnError,
)

/** BlueRadiance theme */
val BlueRadianceColorScheme = darkColorScheme(
    primary = BlueRadiancePrimary,
    onPrimary = LightBackground,
    background = BlueRadianceBackground,
    onBackground = DarkOnBackground,
    surface = BlueRadianceSurface,
    onSurface = DarkOnSurface,
    error = MulletaFlixError,
    onError = MulletaFlixOnError,
)

/**
 * WMC theme (Windows Media Center).
 *
 * Shares the cinematic dark base with [DarkColorScheme] and tints the chrome blue, which
 * is what Windows Media Center's own shell looked like. It used to map straight to
 * [DarkColorScheme], so choosing "WMC" in Ajustes changed nothing at all — the screen
 * claimed a theme the app did not apply.
 *
 * The accent stays the brand red: the promise is a tint on the surfaces, not a different
 * brand.
 */
val WmcColorScheme = darkColorScheme(
    primary = MulletaFlixBlack,
    onPrimary = Color.White,
    primaryContainer = MulletaFlixRedDark,
    onPrimaryContainer = Color.White,
    secondary = MulletaFlixRedAccessible,
    onSecondary = Color.White,
    secondaryContainer = MulletaFlixRedDark,
    onSecondaryContainer = Color.White,
    background = WmcBackground,
    onBackground = DarkOnBackground,
    surface = WmcSurface,
    onSurface = DarkOnSurface,
    surfaceVariant = WmcSurfaceVariant,
    onSurfaceVariant = DarkOnSurfaceVariant,
    outline = DarkOutline,
    outlineVariant = DarkOutlineVariant,
    error = MulletaFlixError,
    onError = MulletaFlixOnError,
    errorContainer = MulletaFlixErrorContainer,
    onErrorContainer = MulletaFlixOnErrorContainer,
)

/**
 * AppleTV theme.
 *
 * tvOS keeps a near-black, slightly cool and slightly lighter chrome than a pure black
 * cinematic surface. Same story as [WmcColorScheme]: it was an alias for the dark theme.
 */
val AppleTvColorScheme = darkColorScheme(
    primary = MulletaFlixBlack,
    onPrimary = Color.White,
    primaryContainer = MulletaFlixRedDark,
    onPrimaryContainer = Color.White,
    secondary = MulletaFlixRedAccessible,
    onSecondary = Color.White,
    secondaryContainer = MulletaFlixRedDark,
    onSecondaryContainer = Color.White,
    background = AppleTvBackground,
    onBackground = DarkOnBackground,
    surface = AppleTvSurface,
    onSurface = DarkOnSurface,
    surfaceVariant = AppleTvSurfaceVariant,
    onSurfaceVariant = DarkOnSurfaceVariant,
    outline = DarkOutline,
    outlineVariant = DarkOutlineVariant,
    error = MulletaFlixError,
    onError = MulletaFlixOnError,
    errorContainer = MulletaFlixErrorContainer,
    onErrorContainer = MulletaFlixOnErrorContainer,
)
