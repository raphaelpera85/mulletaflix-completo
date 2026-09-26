package org.mulletaflix.domain.model

/**
 * Canonical subtitle styling: which colours exist and what size range is valid.
 *
 * This lived in `:design-system`, which was the wrong home for two reasons:
 *
 *  - `:data` could not reach it, so `SettingsRepositoryImpl` kept its **own** copy of
 *    the accepted colours and of the `50..200` bound. Bounds that agree by hand are
 *    the exact shape that has already produced bugs in this project (theme enums, sort
 *    labels, language codes, subtitle colour codes);
 *  - the values are not a UI concern. They are what the app stores and what the server
 *    receives, next to [MediaLanguage], which solved the same problem for languages.
 *
 * `:design-system` keeps only the Compose-dependent part — turning a code into a
 * `Color` — and reads these constants.
 */

/** Codes the app stores for subtitle colour. */
const val SUBTITLE_COLOR_WHITE = "WHITE"
const val SUBTITLE_COLOR_YELLOW = "YELLOW"
const val SUBTITLE_COLOR_CYAN = "CYAN"

/** Every colour the app offers, in display order. */
val subtitleColorCodes: List<String> = listOf(
    SUBTITLE_COLOR_WHITE,
    SUBTITLE_COLOR_YELLOW,
    SUBTITLE_COLOR_CYAN,
)

/** Smallest and largest subtitle size the app accepts, in percent. */
const val MIN_SUBTITLE_SIZE_PERCENT = 50
const val MAX_SUBTITLE_SIZE_PERCENT = 200

/**
 * Media3's `subtitleView` size for a stored percentage sits at this fraction of the
 * view's height.
 *
 * The value is fractional — relative to the video's height — which is what lets the
 * settings preview show the same *relative* progression in a much smaller box. Keeping
 * the multiplier here is what makes "150%" mean the same thing in both places.
 */
private const val SUBTITLE_FRACTIONAL_SIZE_AT_100_PERCENT = 0.0533f

/** Accepts any stored colour, falling back to the default rather than failing. */
fun normalizeSubtitleColor(value: String?): String = when (value?.trim()?.uppercase()) {
    SUBTITLE_COLOR_YELLOW -> SUBTITLE_COLOR_YELLOW
    SUBTITLE_COLOR_CYAN -> SUBTITLE_COLOR_CYAN
    else -> SUBTITLE_COLOR_WHITE
}

/** Clamps a stored subtitle size into the range the app supports. */
fun normalizeSubtitleSizePercent(percent: Int): Int =
    percent.coerceIn(MIN_SUBTITLE_SIZE_PERCENT, MAX_SUBTITLE_SIZE_PERCENT)

/** Fractional text size for a stored percentage, as Media3 expects it. */
fun subtitleFractionalTextSize(percent: Int): Float =
    SUBTITLE_FRACTIONAL_SIZE_AT_100_PERCENT * normalizeSubtitleSizePercent(percent) / 100f
