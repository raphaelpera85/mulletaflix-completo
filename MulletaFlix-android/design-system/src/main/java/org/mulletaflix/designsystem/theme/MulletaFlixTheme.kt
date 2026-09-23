package org.mulletaflix.designsystem.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.ColorScheme
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
 * The colour scheme a variant pins, or `null` when only the device can decide
 * ([MulletaFlixThemeVariant.System] on Android 12+).
 *
 * This is a function instead of a `when` inside the composable so that a test can
 * assert the **mapping**. Two themes pointing at the same scheme is a mapping bug,
 * and a test that compared colour constants would not see it: it would pass with
 * the defect still in place.
 */
internal fun staticColorSchemeFor(variant: MulletaFlixThemeVariant): ColorScheme? = when (variant) {
    MulletaFlixThemeVariant.System -> null
    MulletaFlixThemeVariant.Dark -> DarkColorScheme
    MulletaFlixThemeVariant.Light -> LightColorScheme
    MulletaFlixThemeVariant.Netflix -> NetflixColorScheme
    MulletaFlixThemeVariant.PurpleHaze -> PurpleHazeColorScheme
    MulletaFlixThemeVariant.BlueRadiance -> BlueRadianceColorScheme
    // The same cinematic dark base as Dark, with the chrome tinted — see the schemes.
    MulletaFlixThemeVariant.WMC -> WmcColorScheme
    MulletaFlixThemeVariant.AppleTV -> AppleTvColorScheme
}

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

    val baseScheme = staticColorSchemeFor(variant) ?: if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
        if (isDarkSystem) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
    } else {
        if (isDarkSystem) DarkColorScheme else LightColorScheme
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
