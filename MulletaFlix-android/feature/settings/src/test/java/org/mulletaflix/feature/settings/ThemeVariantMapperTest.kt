package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.domain.repository.AppThemeSetting

/**
 * Guards the single mapping between the persisted theme and the visual variant.
 *
 * The defect this protects against: the settings screen kept its own copy of the
 * mapping while the app root called `MulletaFlixTheme()` with no variant, so
 * every theme except Dark was saved, displayed and then never applied. A
 * round-trip test is the cheap way to notice the two sides disagreeing.
 */
class ThemeVariantMapperTest {

    @Test
    fun `every persisted theme maps to a variant`() {
        val mapped = AppThemeSetting.entries.map { it.toThemeVariant() }

        assertEquals(
            "each persisted theme needs its own visual variant",
            AppThemeSetting.entries.size,
            mapped.toSet().size,
        )
    }

    @Test
    fun `every variant maps back to a persisted theme`() {
        val mapped = MulletaFlixThemeVariant.entries.map { it.toAppThemeSetting() }

        assertEquals(
            "each visual variant needs a persisted counterpart",
            MulletaFlixThemeVariant.entries.size,
            mapped.toSet().size,
        )
    }

    @Test
    fun `the mapping is a round trip in both directions`() {
        AppThemeSetting.entries.forEach { setting ->
            assertEquals(
                "AppThemeSetting.$setting did not survive the round trip",
                setting,
                setting.toThemeVariant().toAppThemeSetting(),
            )
        }
        MulletaFlixThemeVariant.entries.forEach { variant ->
            assertEquals(
                "MulletaFlixThemeVariant.$variant did not survive the round trip",
                variant,
                variant.toAppThemeSetting().toThemeVariant(),
            )
        }
    }

    @Test
    fun `the brand identity of each named theme is preserved`() {
        // Not just bijective: each value must map to the theme it names,
        // otherwise a correct round trip could still paint the wrong colours.
        assertEquals(MulletaFlixThemeVariant.Light, AppThemeSetting.Light.toThemeVariant())
        assertEquals(MulletaFlixThemeVariant.Netflix, AppThemeSetting.Netflix.toThemeVariant())
        assertEquals(MulletaFlixThemeVariant.PurpleHaze, AppThemeSetting.PurpleHaze.toThemeVariant())
        assertEquals(MulletaFlixThemeVariant.BlueRadiance, AppThemeSetting.BlueRadiance.toThemeVariant())
        assertEquals(MulletaFlixThemeVariant.WMC, AppThemeSetting.Wmc.toThemeVariant())
        assertEquals(MulletaFlixThemeVariant.AppleTV, AppThemeSetting.AppleTv.toThemeVariant())
        assertEquals(MulletaFlixThemeVariant.System, AppThemeSetting.DynamicColor.toThemeVariant())
    }
}
