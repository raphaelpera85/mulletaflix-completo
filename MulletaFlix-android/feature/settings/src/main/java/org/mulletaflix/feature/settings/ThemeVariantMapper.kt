package org.mulletaflix.feature.settings

import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.domain.repository.AppThemeSetting

/**
 * Single mapping between the persisted theme and the visual variant.
 *
 * It lives here because this module already depends on both `:domain` (which
 * owns [AppThemeSetting]) and `:design-system` (which owns
 * [MulletaFlixThemeVariant]), and because the app root already depends on this
 * module.
 *
 * The duplication this replaces was a real defect: the settings screen kept a
 * private copy of this `when` while the app root called `MulletaFlixTheme()`
 * with no variant. Every theme except Dark was therefore saved, displayed in
 * Settings, and never applied — the app stayed permanently dark. Keeping one
 * mapping is what stops the two from drifting again.
 */
fun AppThemeSetting.toThemeVariant(): MulletaFlixThemeVariant = when (this) {
    AppThemeSetting.Dark -> MulletaFlixThemeVariant.Dark
    AppThemeSetting.Light -> MulletaFlixThemeVariant.Light
    AppThemeSetting.Netflix -> MulletaFlixThemeVariant.Netflix
    AppThemeSetting.PurpleHaze -> MulletaFlixThemeVariant.PurpleHaze
    AppThemeSetting.BlueRadiance -> MulletaFlixThemeVariant.BlueRadiance
    AppThemeSetting.Wmc -> MulletaFlixThemeVariant.WMC
    AppThemeSetting.AppleTv -> MulletaFlixThemeVariant.AppleTV
    AppThemeSetting.DynamicColor -> MulletaFlixThemeVariant.System
}

/** Inverse of [toThemeVariant], used when the Settings screen writes a choice. */
fun MulletaFlixThemeVariant.toAppThemeSetting(): AppThemeSetting = when (this) {
    MulletaFlixThemeVariant.System -> AppThemeSetting.DynamicColor
    MulletaFlixThemeVariant.Dark -> AppThemeSetting.Dark
    MulletaFlixThemeVariant.Light -> AppThemeSetting.Light
    MulletaFlixThemeVariant.Netflix -> AppThemeSetting.Netflix
    MulletaFlixThemeVariant.PurpleHaze -> AppThemeSetting.PurpleHaze
    MulletaFlixThemeVariant.BlueRadiance -> AppThemeSetting.BlueRadiance
    MulletaFlixThemeVariant.WMC -> AppThemeSetting.Wmc
    MulletaFlixThemeVariant.AppleTV -> AppThemeSetting.AppleTv
}
