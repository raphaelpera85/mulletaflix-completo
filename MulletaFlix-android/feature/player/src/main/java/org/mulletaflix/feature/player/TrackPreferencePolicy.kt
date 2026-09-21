package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaStream

/** Chooses the server stream index that best matches a persisted language preference. */
fun preferredStreamIndex(
    streams: List<MediaStream>,
    preferredLanguage: String?,
    serverDefaultIndex: Int?,
): Int? {
    if (preferredLanguage.equals("off", ignoreCase = true) ||
        preferredLanguage.equals("none", ignoreCase = true)
    ) {
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

/** Returns only an explicit language match suitable for the initial server request. */
fun requestedPreferredStreamIndex(
    streams: List<MediaStream>,
    preferredLanguage: String?,
): Int? {
    if (preferredLanguage.equals("off", ignoreCase = true) ||
        preferredLanguage.equals("none", ignoreCase = true)
    ) {
        return null
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

private fun canonicalLanguage(value: String): String {
    val normalized = value
        .lowercase()
        .replace('_', '-')
        .substringBefore('(')
        .trim()

    return when (normalized) {
    "por", "pt", "pt-br", "pt-pt", "portuguese", "português" -> "pt"
    "eng", "en", "en-us", "en-gb", "english", "inglês" -> "en"
    "spa", "es", "es-es", "spanish", "espanhol" -> "es"
    "fra", "fr", "french", "francês" -> "fr"
    "deu", "de", "german", "alemão" -> "de"
    else -> normalized
    }
}
