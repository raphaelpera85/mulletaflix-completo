package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaLanguage

import org.mulletaflix.domain.model.MediaStream

/**
 * True when the stored preference means "no subtitles".
 *
 * Goes through the shared [MediaLanguage] catalogue instead of comparing two
 * literals, so every spelling the app can store or display — `off`, `none`,
 * `Desativadas`, `desabilitadas`, any case — means the same thing. The old
 * literal comparison missed the label the settings screen shows.
 */
internal fun prefersNoSubtitles(preferredLanguage: String?): Boolean =
    MediaLanguage.canonicalize(preferredLanguage) == MediaLanguage.OFF

/** Chooses the server stream index that best matches a persisted language preference. */
fun preferredStreamIndex(
    streams: List<MediaStream>,
    preferredLanguage: String?,
    serverDefaultIndex: Int?,
): Int? {
    if (prefersNoSubtitles(preferredLanguage)) {
        return null
    }

    val normalizedPreference = preferredLanguage
        ?.trim()
        ?.let(::canonicalLanguage)
        ?.takeIf { it.isNotBlank() && it != "original" }

    matchingStreamIndex(streams, normalizedPreference)?.let { return it }

    serverDefaultIndex?.let { defaultIndex ->
        if (streams.any { it.index == defaultIndex }) return defaultIndex
    }
    return streams.firstOrNull { it.isDefault }?.index ?: streams.firstOrNull()?.index
}

/**
 * The subtitle stream index Jellyfin reads as "no subtitles at all".
 *
 * Confirmed against the server: `MediaSourceManager.SetDefaultSubtitleStreamIndex`
 * accepts `index == -1` as a remembered "none" and returns immediately with
 * `DefaultSubtitleStreamIndex = -1`, while `EncodingHelper` only burns in a
 * subtitle when `SubtitleStreamIndex >= 0` and the DLNA stream builder tests
 * `SubtitleStreamIndex != -1`.
 */
const val DISABLED_SUBTITLE_STREAM_INDEX = -1

/** Returns only an explicit language match suitable for the initial server request. */
fun requestedPreferredStreamIndex(
    streams: List<MediaStream>,
    preferredLanguage: String?,
): Int? {
    if (prefersNoSubtitles(preferredLanguage)) {
        // Not null: null means "no preference" to the server, which then picks a
        // default from the user's server-side subtitle mode and may burn one in.
        // "Off" only actually turns subtitles off when it is stated as -1.
        return DISABLED_SUBTITLE_STREAM_INDEX
    }

    val normalizedPreference = preferredLanguage
        ?.trim()
        ?.let(::canonicalLanguage)
        ?.takeIf { it.isNotBlank() && it != "original" }

    return matchingStreamIndex(streams, normalizedPreference)
}

private fun matchingStreamIndex(streams: List<MediaStream>, normalizedPreference: String?): Int? {
    if (normalizedPreference == null) return null
    return streams.firstOrNull { stream ->
        val language = stream.language?.let(::canonicalLanguage)
        val displayLanguage = stream.displayLanguage?.let(::canonicalLanguage)
        language == normalizedPreference || displayLanguage == normalizedPreference ||
            language?.startsWith("$normalizedPreference-") == true
    }?.index
}

/**
 * Delegates to the shared catalogue in `:domain`.
 *
 * This list used to live here as a private copy, which is exactly how the
 * player and the settings screen came to disagree about languages: the player
 * canonicalised `spa` to `es` while the settings screen displayed everything
 * beyond Portuguese and English as "Idioma original".
 */
private fun canonicalLanguage(value: String): String = MediaLanguage.canonicalize(value)

/**
 * The language worth persisting for a track the user just chose, or null.
 *
 * A server may describe a track without any language at all. Persisting that
 * `null` removed the stored preference — `SettingsRepositoryImpl` deletes the key
 * when the value is blank and then answers with its default, `"por"` — so the
 * next item silently enabled the default track instead of the one the user had
 * chosen. Returning null here means "there is nothing better to record", and the
 * previous preference is kept.
 *
 * The choice is still applied to the item being watched either way; this only
 * decides what is remembered for the following one.
 */
internal fun persistableTrackLanguage(language: String?): String? =
    language?.trim()?.takeIf { it.isNotBlank() }
