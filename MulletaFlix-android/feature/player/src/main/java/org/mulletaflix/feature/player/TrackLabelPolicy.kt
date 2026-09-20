package org.mulletaflix.feature.player

/** Converts bare ISO/server language codes into readable names without replacing explicit titles. */
internal fun friendlyTrackName(
    displayName: String?,
    language: String?,
    fallback: String,
): String {
    val candidate = displayName?.trim().takeUnless { it.isNullOrBlank() }
        ?: language?.trim().takeUnless { it.isNullOrBlank() }
        ?: return fallback

    return languageCodeLabel(candidate) ?: candidate
}

private fun languageCodeLabel(value: String): String? = when (
    value.lowercase().replace('_', '-').trim()
) {
    "por", "pt" -> "Português"
    "pt-br" -> "Português (Brasil)"
    "pt-pt" -> "Português (Portugal)"
    "eng", "en" -> "English"
    "en-us" -> "English (US)"
    "en-gb" -> "English (UK)"
    "spa", "es" -> "Español"
    "fra", "fre", "fr" -> "Français"
    "deu", "ger", "de" -> "Deutsch"
    "ita", "it" -> "Italiano"
    "jpn", "ja" -> "日本語"
    "kor", "ko" -> "한국어"
    "zho", "chi", "zh" -> "中文"
    "rus", "ru" -> "Русский"
    "ara", "ar" -> "العربية"
    else -> null
}

/** Presents stream metadata without exposing server-specific field names. */
internal fun trackLabel(track: TrackInfo): String = buildList {
    add(track.displayName)
    track.codec
        ?.trim()
        ?.takeIf { it.isNotEmpty() }
        ?.uppercase()
        ?.let(::add)
    track.channels?.let { channels ->
        when {
            channels >= 8 -> add("7.1")
            channels >= 6 -> add("5.1")
            channels >= 2 -> add("Estéreo")
            channels == 1 -> add("Mono")
        }
    }
    if (track.isDefault) add("Padrão")
    if (track.isForced) add("Forçada")
}.joinToString(" • ")
