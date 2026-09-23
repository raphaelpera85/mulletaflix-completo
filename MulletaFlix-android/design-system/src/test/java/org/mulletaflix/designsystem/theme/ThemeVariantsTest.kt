package org.mulletaflix.designsystem.theme

import androidx.compose.material3.ColorScheme
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Cada tema oferecido em Ajustes precisa mudar alguma coisa.
 *
 * O defeito: `WMC` e `AppleTV` mapeavam direto para `DarkColorScheme`, então escolher
 * "Apple TV" deixava a interface idêntica a "Escuro (Padrão)" enquanto a tela afirmava o
 * contrário. Um tema que não faz nada é pior do que um tema que não existe.
 */
class ThemeVariantsTest {

    /**
     * Os temas estáticos; `System` depende do aparelho e do Android 12+.
     *
     * A lista vem do **mapeamento** (`staticColorSchemeFor`), não das constantes: o
     * defeito era o mapeamento apontar duas variantes para o mesmo esquema, e um teste
     * que comparasse as constantes passaria com o defeito no lugar.
     */
    private val variants: List<MulletaFlixThemeVariant> = listOf(
        MulletaFlixThemeVariant.Dark,
        MulletaFlixThemeVariant.Light,
        MulletaFlixThemeVariant.Netflix,
        MulletaFlixThemeVariant.PurpleHaze,
        MulletaFlixThemeVariant.BlueRadiance,
        MulletaFlixThemeVariant.WMC,
        MulletaFlixThemeVariant.AppleTV,
    )

    private fun schemeFor(variant: MulletaFlixThemeVariant): ColorScheme =
        requireNotNull(staticColorSchemeFor(variant)) { "$variant não tem esquema estático" }

    /** The colours a viewer actually sees change when they pick a theme. */
    private fun visible(variant: MulletaFlixThemeVariant): List<Any> =
        schemeFor(variant).let { scheme ->
            listOf(
                scheme.background,
                scheme.surface,
                scheme.surfaceVariant,
                scheme.primary,
                scheme.secondary,
            )
        }

    @Test
    fun `no two themes look the same`() {
        variants.forEachIndexed { index, variant ->
            for (other in variants.drop(index + 1)) {
                assertNotEquals(
                    "\"${variant.name}\" e \"${other.name}\" pintam a mesma tela",
                    visible(variant),
                    visible(other),
                )
            }
        }
    }

    @Test
    fun `the system variant is the only one resolved by the device`() {
        assertTrue(
            "System precisa ficar para o caminho dinâmico do composable",
            staticColorSchemeFor(MulletaFlixThemeVariant.System) == null,
        )
        assertTrue(variants.isEmpty() || staticColorSchemeFor(MulletaFlixThemeVariant.Dark) != null)
    }

    @Test
    fun `every dark theme keeps the lifted accent readable on its own surface`() {
        // O acento é elevado por `withAccessibleAccent()` contra a superfície do próprio
        // tema; uma superfície nova (WMC, AppleTV) tem de continuar passando.
        variants
            .filter { it != MulletaFlixThemeVariant.Light }
            .forEach { variant ->
                val accessible = schemeFor(variant).withAccessibleAccent()
                val ratio = contrastRatio(accessible.secondary, accessible.surface)
                assertTrue(
                    "o acento de ${variant.name} mede ${"%.2f".format(ratio)}:1 na própria superfície",
                    ratio >= 4.5,
                )
            }
    }

    @Test
    fun `the tinted themes keep their outline visible`() {
        // WCAG 2.2 SC 1.4.11: a borda é o que identifica um campo, então precisa de 3:1
        // contra a superfície adjacente.
        listOf(MulletaFlixThemeVariant.WMC, MulletaFlixThemeVariant.AppleTV).forEach { variant ->
            val scheme = schemeFor(variant)
            val ratio = contrastRatio(scheme.outline, scheme.surface)
            assertTrue(
                "a borda de ${variant.name} mede ${"%.2f".format(ratio)}:1 na superfície",
                ratio >= 3.0,
            )
        }
    }
}
