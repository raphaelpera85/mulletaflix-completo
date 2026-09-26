package org.mulletaflix.feature.settings

import org.mulletaflix.domain.model.LibrarySortField
import org.mulletaflix.domain.model.MAX_SUBTITLE_SIZE_PERCENT
import org.mulletaflix.domain.model.MIN_SUBTITLE_SIZE_PERCENT
import org.mulletaflix.domain.model.MediaLanguage
import org.mulletaflix.domain.model.SUBTITLE_COLOR_WHITE
import org.mulletaflix.domain.model.SUBTITLE_COLOR_YELLOW
import org.mulletaflix.domain.model.SUBTITLE_COLOR_CYAN

/**
 * The option lists the settings dialogs offer, derived from the same catalogues
 * the rest of the app stores values with.
 *
 * They used to be literals inside the composables, which is the recurring defect
 * this codebase keeps producing: the same value list maintained in two places.
 * The screen offered five of the eight [LibrarySortField] entries, so "Aleatório",
 * "Mais Assistidos" and "Assistido Recentemente" could not be chosen at all, and
 * only Portuguese and English of the five languages [MediaLanguage] knows. A
 * field the dialog cannot show is a field the user cannot select — the catalogue
 * was fixed but the dialog was not.
 *
 * Deriving the list means adding a language or a sort field to the catalogue
 * makes it selectable here without touching this screen, and
 * `SettingsOptionListsTest` fails if the two ever drift again.
 */

/** A value the settings screen shows, paired with what it stores. */
internal data class LabeledChoice(val label: String, val code: String)

/** Every sort field the library screen can store, in display order. */
internal val librarySortLabels: List<String> = LibrarySortField.entries.map { it.label }

/**
 * The dialog's options, with [current] appended when the catalogue does not know it.
 *
 * A value the app can store but the dialog cannot show is a value the viewer cannot
 * change back: no row matches (so nothing looks selected) and confirming anything else
 * silently replaces the real preference. Quality and language both hit this — the player
 * stores whatever the server offers, while these lists are a catalogue of the common
 * ones.
 */
internal fun choicesIncludingCurrent(choices: List<String>, current: String?): List<String> {
    val trimmed = current?.trim().orEmpty()
    if (trimmed.isEmpty()) return choices
    if (choices.any { it.equals(trimmed, ignoreCase = true) }) return choices
    return choices + trimmed
}

/** Languages offered for audio, plus the "server default" pseudo-language. */
internal val audioLanguageLabels: List<String> =
    MediaLanguage.selectable.map { it.label } + MediaLanguage.label(MediaLanguage.ORIGINAL)

/** Languages offered for subtitles, plus the server default and "no subtitles". */
internal val subtitleLanguageLabels: List<String> =
    MediaLanguage.selectable.map { it.label } +
        MediaLanguage.label(MediaLanguage.ORIGINAL) +
        MediaLanguage.label(MediaLanguage.OFF)

/**
 * Grid densities, in display order.
 *
 * The label→code mapping lives here with the labels. It used to live in the
 * view model while the labels lived in the dialog, so the two could disagree and
 * a confirmed choice would silently store the fallback.
 */
internal val libraryGridDensityChoices: List<LabeledChoice> = listOf(
    LabeledChoice(label = "Confortável", code = LIBRARY_GRID_DENSITY_COMFORTABLE),
    LabeledChoice(label = "Compacta", code = LIBRARY_GRID_DENSITY_COMPACT),
)

/** Subtitle colours, in display order. */
internal val subtitleColorChoices: List<LabeledChoice> = listOf(
    LabeledChoice(label = "Branco", code = SUBTITLE_COLOR_WHITE),
    LabeledChoice(label = "Amarelo", code = SUBTITLE_COLOR_YELLOW),
    LabeledChoice(label = "Ciano", code = SUBTITLE_COLOR_CYAN),
)

/** Sort directions, in display order. */
internal val librarySortOrderChoices: List<LabeledChoice> = listOf(
    LabeledChoice(label = "Ascendente", code = LIBRARY_SORT_ORDER_ASCENDING),
    LabeledChoice(label = "Descendente", code = LIBRARY_SORT_ORDER_DESCENDING),
)

internal const val LIBRARY_GRID_DENSITY_COMFORTABLE = "COMFORTABLE"
internal const val LIBRARY_GRID_DENSITY_COMPACT = "COMPACT"
internal const val LIBRARY_SORT_ORDER_ASCENDING = "Ascending"
internal const val LIBRARY_SORT_ORDER_DESCENDING = "Descending"

/** Playback speeds the dialog offers, in display order. */
internal val playbackSpeedChoices: List<Float> = listOf(0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f)

/**
 * Subtitle font sizes the dialog offers, in display order.
 *
 * The smallest entry is the smallest size the data layer accepts
 * (`MIN_SUBTITLE_SIZE_PERCENT`): a value the repository preserves but the dialog cannot
 * show is a value the viewer cannot choose again.
 */
internal val subtitleFontSizeChoices: List<Int> =
    listOf(MIN_SUBTITLE_SIZE_PERCENT, 75, 100, 125, 150, MAX_SUBTITLE_SIZE_PERCENT)

/** How a speed is written in the dialog and in the settings row. */
internal fun playbackSpeedLabel(speed: Float): String = speed.toString()

/** How a font size is written in the dialog and in the settings row. */
internal fun subtitleFontSizeLabel(size: Int): String = size.toString()

/** Resolves a displayed grid density to the stored code. */
internal fun libraryGridDensityCode(label: String?): String =
    libraryGridDensityChoices.firstOrNull { it.label == label }?.code
        ?: LIBRARY_GRID_DENSITY_COMFORTABLE

/** Resolves a displayed subtitle colour to the stored code. */
internal fun subtitleColorCode(label: String?): String =
    subtitleColorChoices.firstOrNull { it.label == label }?.code ?: SUBTITLE_COLOR_WHITE

/** Resolves a stored grid density code to its label. */
internal fun libraryGridDensityLabel(code: String?): String =
    libraryGridDensityChoices.firstOrNull { it.code.equals(code?.trim(), ignoreCase = true) }?.label
        ?: libraryGridDensityChoices.first().label

/** Resolves a stored subtitle colour code to its label. */
internal fun subtitleColorLabel(code: String?): String =
    subtitleColorChoices.firstOrNull { it.code.equals(code?.trim(), ignoreCase = true) }?.label
        ?: subtitleColorChoices.first().label

/** Resolves a displayed sort direction to the stored code. */
internal fun librarySortOrderCode(label: String?): String =
    librarySortOrderChoices.firstOrNull { it.label == label }?.code ?: LIBRARY_SORT_ORDER_ASCENDING

/** Resolves a stored sort direction code to its label. */
internal fun librarySortOrderLabel(code: String?): String =
    librarySortOrderChoices.firstOrNull { it.code.equals(code?.trim(), ignoreCase = true) }?.label
        ?: librarySortOrderChoices.first().label
