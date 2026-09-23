package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A regra de qualidade tinha duas cópias: o player guarda a resolução que o título
 * realmente oferece ("360p", "576p"), e a tela de Ajustes conhecia seis presets e
 * respondia "Automático" para o resto — o valor guardado ficava invisível e não podia ser
 * reescolhido. Uma definição só, aqui.
 */
class PlaybackQualityPolicyTest {

    @Test
    fun `the presets are the ones the settings dialog offers`() {
        assertEquals(listOf("4K", "1440p", "1080p", "720p", "480p"), QUALITY_PRESET_CHOICES)
        assertFalse("Auto não é um preset, é o modo adaptativo", QUALITY_PRESET_CHOICES.contains(QUALITY_AUTO))
    }

    @Test
    fun `a resolution a title really offers is kept`() {
        listOf("360p", "576p", "144p", "4320p", "900p").forEach { value ->
            assertEquals(value, normalizePlaybackQualityPreference(value))
        }
        assertEquals("360p", normalizePlaybackQualityPreference("  360P  "))
    }

    @Test
    fun `every spelling an older version may have written collapses to one code`() {
        assertEquals("Auto", normalizePlaybackQualityPreference("Automático"))
        assertEquals("Auto", normalizePlaybackQualityPreference("automatico"))
        assertEquals("Auto", normalizePlaybackQualityPreference(null))
        assertEquals("4K", normalizePlaybackQualityPreference("2160"))
        assertEquals("1080p", normalizePlaybackQualityPreference("FULL HD"))
        assertEquals("720p", normalizePlaybackQualityPreference("HD"))
        assertEquals("480p", normalizePlaybackQualityPreference("SD"))
    }

    @Test
    fun `junk is answered with Auto instead of being stored`() {
        listOf("9999p", "0p", "360", "360px", "p", "lixo", "-360p").forEach { value ->
            assertEquals(
                "\"$value\" não é uma preferência aceitável",
                QUALITY_AUTO,
                normalizePlaybackQualityPreference(value),
            )
        }
    }

    @Test
    fun `the label for a height is the name the dialog and the server use`() {
        assertEquals("4K", qualityLabelForHeight(2160))
        assertEquals("1440p", qualityLabelForHeight(1440))
        assertEquals("1080p", qualityLabelForHeight(1080))
        assertEquals("720p", qualityLabelForHeight(720))
        assertEquals("480p", qualityLabelForHeight(480))
        assertEquals("360p", qualityLabelForHeight(360))
        assertEquals("144p", qualityLabelForHeight(144))
    }

    @Test
    fun `a preset is told apart from a resolution that is not one`() {
        assertTrue(isPlaybackQualityPreset("1080p"))
        assertTrue(isPlaybackQualityPreset("4K"))
        assertFalse(
            "360p é uma preferência válida, mas o diálogo precisa criar uma linha para ela",
            isPlaybackQualityPreset("360p"),
        )
        assertFalse(isPlaybackQualityPreset("Auto"))
    }
}
