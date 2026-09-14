package org.mulletaflix.designsystem.theme

import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.ui.graphics.Color

/** Default dark theme — preto cinematográfico com vermelho MulletaFlix */
val DarkColorScheme = darkColorScheme(
    primary = MulletaFlixBlack,
    onPrimary = Color.White,
    primaryContainer = MulletaFlixRedDark,
    onPrimaryContainer = Color.White,
    secondary = MulletaFlixRed,
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

/** Light theme */
val LightColorScheme = lightColorScheme(
    primary = MulletaFlixRed,
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
