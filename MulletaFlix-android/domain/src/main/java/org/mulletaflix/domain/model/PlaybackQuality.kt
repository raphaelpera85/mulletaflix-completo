package org.mulletaflix.domain.model

/**
 * The one definition of what a playback-quality preference is.
 *
 * There were two, and they disagreed. The player accepts whatever resolution a real
 * title offers — a 360p track is named `360p`, a 576p track `576p` — and stores that
 * value. The settings screen only knew six presets and answered `Auto` for anything
 * else, so choosing 360p while watching made Ajustes show "Automático" and the quality
 * dialog had no row for the value that was actually stored: the real preference was
 * invisible and could not be selected again.
 *
 * Keeping the presets and the rule here means the dialog, the settings row and the
 * player all read the same definition.
 */

/** Stored value meaning "let the player adapt to the network". */
const val QUALITY_AUTO = "Auto"

/**
 * Height of the smallest and largest resolution accepted as a quality preference.
 *
 * Anything outside is junk rather than a preference — and junk is answered with
 * [QUALITY_AUTO], never stored.
 */
const val MIN_QUALITY_HEIGHT = 144
const val MAX_QUALITY_HEIGHT = 4320

/** Resolutions the settings dialog offers, largest first. */
val QUALITY_PRESET_CHOICES: List<String> = listOf("4K", "1440p", "1080p", "720p", "480p")

/**
 * Canonical label for a real video height.
 *
 * `2160` is presented as "4K" and `1440` as "1440p" because those are the names the
 * settings dialog and the server both use. Everything else keeps its own height — a
 * 360p or 576p stream exists on real servers and must be nameable.
 */
fun qualityLabelForHeight(height: Int): String = when {
    height >= 2160 -> "4K"
    height >= 1440 -> "1440p"
    height >= 1080 -> "1080p"
    height >= 720 -> "720p"
    height >= 480 -> "480p"
    else -> "${height}p"
}

/**
 * Keeps the persisted quality preference valid when it comes from UI or old storage.
 *
 * Every spelling the app or an older version may have written collapses into one code;
 * a plausible height written as `NNNp` is preserved instead of discarded. A bare number
 * (`360`) is not a form this app produces, so it is junk like any other unknown value.
 */
fun normalizePlaybackQualityPreference(value: String?): String {
    val raw = value?.trim()?.uppercase().orEmpty()
    return when (raw) {
        "AUTO", "AUTOMÁTICO", "AUTOMATICO", "" -> QUALITY_AUTO
        "4K", "2160P", "2160" -> "4K"
        "1440P", "1440" -> "1440p"
        "1080P", "1080", "FULL HD" -> "1080p"
        "720P", "720", "HD" -> "720p"
        "480P", "480", "SD" -> "480p"
        else -> heightQualityPreference(raw)
    }
}

/**
 * Whether [value] is a preset the settings dialog already offers.
 *
 * The dialog uses this to decide whether the stored value needs a row of its own; a
 * stored height that is not a preset is still a valid preference and has to be shown.
 */
fun isPlaybackQualityPreset(value: String?): Boolean =
    normalizePlaybackQualityPreference(value) in QUALITY_PRESET_CHOICES

/** Accepts the `NNNp` form the player produces; anything else is junk. */
private fun heightQualityPreference(upperCaseValue: String): String {
    if (!upperCaseValue.endsWith("P")) return QUALITY_AUTO
    val height = upperCaseValue.dropLast(1).toIntOrNull() ?: return QUALITY_AUTO
    return if (height in MIN_QUALITY_HEIGHT..MAX_QUALITY_HEIGHT) "${height}p" else QUALITY_AUTO
}
