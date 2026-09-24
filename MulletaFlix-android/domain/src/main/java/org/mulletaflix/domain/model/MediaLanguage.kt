package org.mulletaflix.domain.model

/**
 * Canonical language handling for audio and subtitle preferences.
 *
 * The stored preference is a language code, but the player saves whatever the
 * server reported (`spa`, `por`, `en`, …) while the settings screen only
 * recognised `por`/`pt`/`eng`/`en` and displayed everything else as "Idioma
 * original". Confirming that value then wrote `original`, so a real choice such
 * as Spanish was silently replaced.
 *
 * One catalogue fixes it from both sides, the same way [LibrarySortField] fixes
 * the library sort: the codes the player writes are the codes this screen reads,
 * and the label is derived from the code rather than kept in a second list.
 */
object MediaLanguage {

    /** Stored value meaning "use whichever track the server marks as default". */
    const val ORIGINAL = "original"

    /** Stored value meaning "no subtitles". */
    const val OFF = "off"

    /** Every language the UI offers, in display order. */
    val selectable: List<Entry> = listOf(
        Entry(canonical = "pt", label = "Português (Brasil)"),
        Entry(canonical = "en", label = "English"),
        Entry(canonical = "es", label = "Español"),
        Entry(canonical = "fr", label = "Français"),
        Entry(canonical = "de", label = "Deutsch"),
        Entry(canonical = "it", label = "Italiano"),
        Entry(canonical = "ja", label = "日本語"),
        Entry(canonical = "ko", label = "한국어"),
        Entry(canonical = "zh", label = "中文"),
        Entry(canonical = "ru", label = "Русский"),
        Entry(canonical = "ar", label = "العربية"),
        Entry(canonical = "nl", label = "Nederlands"),
        Entry(canonical = "tr", label = "Türkçe"),
        Entry(canonical = "pl", label = "Polski"),
        Entry(canonical = "hi", label = "हिन्दी"),
    )

    data class Entry(val canonical: String, val label: String)

    /**
     * Collapses every spelling of a language the app may receive into one code.
     *
     * Unknown input is returned normalised rather than discarded, so a language
     * the server invents still round-trips instead of becoming "original".
     */
    fun canonicalize(value: String?): String {
        val normalized = value
            ?.lowercase()
            ?.replace('_', '-')
            ?.substringBefore('(')
            ?.trim()
            .orEmpty()
        if (normalized.isEmpty()) return ORIGINAL
        return when (normalized) {
            "por", "pt", "pt-br", "pt-pt", "portuguese", "português" -> "pt"
            "eng", "en", "en-us", "en-gb", "english", "inglês" -> "en"
            "spa", "es", "es-es", "es-mx", "spanish", "espanhol", "español" -> "es"
            "fra", "fre", "fr", "fr-fr", "french", "francês", "français" -> "fr"
            "deu", "ger", "de", "de-de", "german", "alemão", "deutsch" -> "de"
            "ita", "it", "it-it", "italian", "italiano" -> "it"
            "jpn", "ja", "ja-jp", "japanese", "日本語" -> "ja"
            "kor", "ko", "ko-kr", "korean", "한국어" -> "ko"
            "zho", "chi", "zh", "zh-cn", "chinese", "中文" -> "zh"
            "rus", "ru", "ru-ru", "russian", "русский" -> "ru"
            "ara", "ar", "ar-sa", "arabic", "العربية" -> "ar"
            "nld", "dut", "nl", "nl-nl", "dutch", "nederlands" -> "nl"
            "tur", "tr", "tr-tr", "turkish", "türkçe" -> "tr"
            "pol", "pl", "pl-pl", "polish", "polski" -> "pl"
            "hin", "hi", "hi-in", "hindi", "हिन्दी" -> "hi"
            "off", "none", "desativadas", "desabilitadas" -> OFF
            "original", "idioma original" -> ORIGINAL
            else -> normalized
        }
    }

    /**
     * Label for a stored code.
     *
     * Original and off have their own words; a known language uses its own name;
     * anything else keeps the reader's attention by naming the code it found
     * instead of pretending it is the original audio. Returning "Idioma
     * original" for an unknown code is what let the settings screen overwrite a
     * real preference.
     */
    fun label(code: String?): String = when (val canonical = canonicalize(code)) {
        ORIGINAL -> "Idioma original"
        OFF -> "Desativadas"
        else -> selectable.firstOrNull { it.canonical == canonical }?.label ?: canonical.uppercase()
    }

    /**
     * Stored code for a label shown in the UI.
     *
     * The inverse of [label] for every value this app produces, including the
     * "unknown code" labels, so confirming the settings dialog preserves the
     * choice instead of resetting it.
     */
    fun code(label: String?): String {
        val trimmed = label?.trim().orEmpty()
        if (trimmed.isEmpty()) return ORIGINAL
        selectable.firstOrNull { it.label.equals(trimmed, ignoreCase = true) }
            ?.let { return it.canonical }
        return canonicalize(trimmed)
    }
}
