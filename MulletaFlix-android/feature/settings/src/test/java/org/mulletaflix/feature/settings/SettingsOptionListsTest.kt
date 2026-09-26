package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.LibrarySortField
import org.mulletaflix.domain.model.MAX_SUBTITLE_SIZE_PERCENT
import org.mulletaflix.domain.model.MIN_SUBTITLE_SIZE_PERCENT
import org.mulletaflix.domain.model.MediaLanguage
import org.mulletaflix.domain.model.QUALITY_AUTO
import org.mulletaflix.domain.model.QUALITY_PRESET_CHOICES
import org.mulletaflix.domain.model.normalizeSubtitleSizePercent

/**
 * The settings dialogs must offer every value the rest of the app can store,
 * and confirming an offered value must not change it.
 *
 * Both halves are real regressions, not hypotheticals:
 *
 *  - the sort dialog listed five of the eight [LibrarySortField] entries, so
 *    "Aleatório", "Mais Assistidos" and "Assistido Recentemente" were unreachable;
 *  - the language dialogs listed two of the five languages [MediaLanguage] knows.
 *
 * A round trip that does not hold is the worse failure: the dialog displays one
 * value, and selecting it stores another. That is how a real language preference
 * used to be silently replaced by "Idioma original".
 *
 * The same test covers the smaller dialogs (grid density, sort direction,
 * subtitle colour, speed, font size) for the same reason: each of them used to
 * be a label list in the composable paired with a mapping somewhere else.
 */
class SettingsOptionListsTest {

    @Test
    fun `the sort dialog offers every field the library can store`() {
        val offered = librarySortLabels.toSet()
        val storable = LibrarySortField.entries.map { it.label }.toSet()

        assertEquals(
            "every sort field the library accepts must be selectable in settings",
            storable,
            offered,
        )
    }

    @Test
    fun `confirming a sort option keeps the field that was displayed`() {
        librarySortLabels.forEach { label ->
            val stored = LibrarySortField.fromLabel(label)
            assertEquals(
                "selecting \"$label\" must store the field it names, not ${stored.name}",
                label,
                stored.label,
            )
        }
    }

    @Test
    fun `both language dialogs offer every language the player can store`() {
        val languages = MediaLanguage.selectable.map { it.label }

        assertTrue(
            "audio is missing ${languages - audioLanguageLabels.toSet()}",
            audioLanguageLabels.containsAll(languages),
        )
        assertTrue(
            "subtitles are missing ${languages - subtitleLanguageLabels.toSet()}",
            subtitleLanguageLabels.containsAll(languages),
        )
    }

    @Test
    fun `confirming a language option keeps the language that was displayed`() {
        (audioLanguageLabels + subtitleLanguageLabels).distinct().forEach { label ->
            assertEquals(
                "selecting \"$label\" must store the language it names",
                label,
                MediaLanguage.label(MediaLanguage.code(label)),
            )
        }
    }

    @Test
    fun `subtitles can be turned off but audio cannot`() {
        assertTrue(
            "subtitles need a way to be disabled",
            subtitleLanguageLabels.contains(MediaLanguage.label(MediaLanguage.OFF)),
        )
        assertFalse(
            "audio has no off state, so offering one would store a value the player cannot honour",
            audioLanguageLabels.contains(MediaLanguage.label(MediaLanguage.OFF)),
        )
        assertTrue(audioLanguageLabels.contains(MediaLanguage.label(MediaLanguage.ORIGINAL)))
        assertTrue(subtitleLanguageLabels.contains(MediaLanguage.label(MediaLanguage.ORIGINAL)))
    }

    @Test
    fun `no dialog lists the same option twice`() {
        assertEquals(librarySortLabels.size, librarySortLabels.distinct().size)
        assertEquals(audioLanguageLabels.size, audioLanguageLabels.distinct().size)
        assertEquals(subtitleLanguageLabels.size, subtitleLanguageLabels.distinct().size)
    }

    @Test
    fun `every labeled choice keeps its own label when it is confirmed`() {
        val catalogues = mapOf(
            "grid density" to libraryGridDensityChoices,
            "subtitle colour" to subtitleColorChoices,
            "sort direction" to librarySortOrderChoices,
        )
        catalogues.forEach { (name, choices) ->
            choices.forEach { choice ->
                val displayed = labelFor(name, choice.code)
                assertEquals(
                    "the stored $name code must display as \"${choice.label}\"",
                    choice.label,
                    displayed,
                )
                assertEquals(
                    "confirming \"${choice.label}\" must store \"${choice.code}\"",
                    choice.code,
                    codeFor(name, choice.label),
                )
            }
            assertEquals(
                "the $name dialog lists a duplicate option",
                choices.size,
                choices.map { it.label }.distinct().size,
            )
        }
    }

    @Test
    fun `every speed the dialog offers survives the repository clamp`() {
        playbackSpeedChoices.forEach { speed ->
            assertEquals(
                "speed $speed must not be rewritten when it is stored",
                speed,
                speed.coerceIn(0.5f, 2f),
            )
            assertEquals(
                "speed $speed must round-trip through the label the dialog shows",
                speed,
                playbackSpeedLabel(speed).toFloat(),
            )
        }
    }

    @Test
    fun `every font size the dialog offers survives the repository clamp`() {
        subtitleFontSizeChoices.forEach { size ->
            assertEquals(
                "font size $size must not be rewritten when it is stored",
                size,
                normalizeSubtitleSizePercent(size),
            )
            assertEquals(
                "font size $size must round-trip through the label the dialog shows",
                size,
                subtitleFontSizeLabel(size).toInt(),
            )
        }
    }

    @Test
    fun `a quality the player can store is shown and kept by the settings screen`() {
        // O player guarda a resolução que o título realmente oferece, então um track de
        // 360p vira "360p". Esta tela só conhecia seis presets e respondia "Automático",
        // deixando o valor real invisível e impossível de reescolher.
        assertEquals("360p", normalizeDefaultQuality("360p"))
        assertEquals("576p", normalizeDefaultQuality("576p"))
        assertEquals("Auto", normalizeDefaultQuality("lixo"))
        defaultQualityChoices.forEach { choice ->
            assertEquals(
                "confirmar \"$choice\" precisa manter o valor exibido",
                choice,
                normalizeDefaultQuality(choice),
            )
        }
    }

    @Test
    fun `the quality dialog keeps its catalogue in step with the domain`() {
        assertEquals(
            "os presets da tela precisam ser os do domínio, na mesma ordem",
            listOf(QUALITY_AUTO) + QUALITY_PRESET_CHOICES,
            defaultQualityChoices,
        )
    }

    @Test
    fun `a stored value outside the catalogue still gets a row in the dialog`() {
        // Sem linha própria o rádio não marca nada e confirmar qualquer outra coisa
        // substitui a preferência real.
        val quality = choicesIncludingCurrent(defaultQualityChoices, "360p")
        assertTrue("360p precisa aparecer", quality.contains("360p"))
        assertEquals("nada pode ser duplicado", quality.size, quality.distinct().size)

        val audio = choicesIncludingCurrent(audioLanguageLabels, "JPN")
        assertTrue("JPN precisa aparecer", audio.contains("JPN"))
        assertEquals(audio.size, audio.distinct().size)
    }

    @Test
    fun `a value the catalogue already knows is not added twice`() {
        assertEquals(
            defaultQualityChoices,
            choicesIncludingCurrent(defaultQualityChoices, "1080p"),
        )
        assertEquals(
            "a comparação ignora maiúsculas",
            defaultQualityChoices,
            choicesIncludingCurrent(defaultQualityChoices, "auto"),
        )
        assertEquals(
            "sem valor guardado a lista fica como está",
            audioLanguageLabels,
            choicesIncludingCurrent(audioLanguageLabels, null),
        )
        assertEquals(
            audioLanguageLabels,
            choicesIncludingCurrent(audioLanguageLabels, "   "),
        )
    }

    @Test
    fun `the smallest subtitle size the repository accepts can be chosen`() {
        // A faixa aceita (50..200) precisa ser alcançável: um valor que a camada de
        // dados preserva e o diálogo não mostra é um valor que o usuário não consegue
        // reescolher.
        assertTrue(
            "o diálogo precisa oferecer o menor tamanho aceito",
            subtitleFontSizeChoices.contains(MIN_SUBTITLE_SIZE_PERCENT),
        )
        assertTrue(
            "e também o maior",
            subtitleFontSizeChoices.contains(MAX_SUBTITLE_SIZE_PERCENT),
        )
        assertEquals(
            "os tamanhos oferecidos precisam estar em ordem",
            subtitleFontSizeChoices.sorted(),
            subtitleFontSizeChoices,
        )
    }

    private fun labelFor(catalogue: String, code: String): String = when (catalogue) {
        "grid density" -> libraryGridDensityLabel(code)
        "subtitle colour" -> subtitleColorLabel(code)
        else -> librarySortOrderLabel(code)
    }

    private fun codeFor(catalogue: String, label: String): String = when (catalogue) {
        "grid density" -> libraryGridDensityCode(label)
        "subtitle colour" -> subtitleColorCode(label)
        else -> librarySortOrderCode(label)
    }
}
