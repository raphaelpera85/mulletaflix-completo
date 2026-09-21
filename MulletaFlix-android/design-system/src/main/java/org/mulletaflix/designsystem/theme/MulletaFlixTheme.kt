package org.mulletaflix.designsystem.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.platform.LocalContext

/**
 * All supported MulletaFlix visual themes, mirroring the 7 web themes.
 * System follows the Android dark/light mode toggle.
 */
enum class MulletaFlixThemeVariant {
    System,
    Dark,
    Light,
    Netflix,
    PurpleHaze,
    BlueRadiance,
    WMC,
    AppleTV
}

val LocalMulletaFlixThemeVariant = staticCompositionLocalOf { MulletaFlixThemeVariant.Dark }

/**
 * Root Material 3 theme for MulletaFlix Android.
 *
 * - Uses Dynamic Color on Android 12+ when theme = System
 * - Falls back to brand-specific color schemes for other themes
 * - Injects Noto Sans typography throughout the app
 */
@Composable
fun MulletaFlixTheme(
    variant: MulletaFlixThemeVariant = MulletaFlixThemeVariant.Dark,
    content: @Composable () -> Unit
) {
    val context = LocalContext.current
    val isDarkSystem = isSystemInDarkTheme()

    val baseScheme = when (variant) {
        MulletaFlixThemeVariant.System -> {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                if (isDarkSystem) dynamicDarkColorScheme(context)
                else dynamicLightColorScheme(context)
            } else {
                if (isDarkSystem) DarkColorScheme else LightColorScheme
            }
        }
        MulletaFlixThemeVariant.Dark -> DarkColorScheme
        MulletaFlixThemeVariant.Light -> LightColorScheme
        MulletaFlixThemeVariant.Netflix -> NetflixColorScheme
        MulletaFlixThemeVariant.PurpleHaze -> PurpleHazeColorScheme
        MulletaFlixThemeVariant.BlueRadiance -> BlueRadianceColorScheme
        // WMC and AppleTV share dark but with slight tints — refined later
        MulletaFlixThemeVariant.WMC -> DarkColorScheme
        MulletaFlixThemeVariant.AppleTV -> DarkColorScheme
    }

    // Every accent label, icon and outline must stay readable on its own
    // surface, so the accent roles are lifted to WCAG AA once, here, instead
    // of relying on each screen to pick a contrast-safe red.
    val colorScheme = baseScheme.withAccessibleAccent()

    CompositionLocalProvider(LocalMulletaFlixThemeVariant provides variant) {
        MaterialTheme(
            colorScheme = colorScheme,
            typography = MulletaFlixTypography,
            content = content
        )
    }
}
