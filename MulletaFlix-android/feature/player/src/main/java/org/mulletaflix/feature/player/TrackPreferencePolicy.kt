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

    if (normalizedPreference != null) {
        streams.firstOrNull { stream ->
            val language = stream.language?.let(::canonicalLanguage)
            val displayLanguage = stream.displayLanguage?.let(::canonicalLanguage)
            language == normalizedPreference || displayLanguage == normalizedPreference ||
                language?.startsWith("$normalizedPreference-") == true
        }?.let { return it.index }
    }

    serverDefaultIndex?.let { defaultIndex ->
        if (streams.any { it.index == defaultIndex }) return defaultIndex
    }
    return streams.firstOrNull { it.isDefault }?.index ?: streams.firstOrNull()?.index
}

private fun canonicalLanguage(value: String): String = when (value.lowercase().replace('_', '-')) {
    "por", "pt", "pt-br", "pt-pt", "portuguese", "português" -> "pt"
    "eng", "en", "en-us", "en-gb", "english", "inglês" -> "en"
    "spa", "es", "es-es", "spanish", "espanhol" -> "es"
    "fra", "fr", "french", "francês" -> "fr"
    "deu", "de", "german", "alemão" -> "de"
    else -> value.lowercase().replace('_', '-')
}
