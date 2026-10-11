package org.mulletaflix.feature.itemdetail

import android.speech.tts.TextToSpeech
import java.util.Locale

internal data class BookSpeechLocaleSelection(
    val locale: Locale,
    val preserveSavedLanguage: Boolean,
)

internal object BookSpeechLocalePolicy {
    fun resolve(
        requestedLanguageTag: String?,
        deviceLocale: Locale,
        languageAvailability: (Locale) -> Int,
    ): BookSpeechLocaleSelection? {
        val requestedLocale = requestedLanguageTag
            ?.takeIf(String::isNotBlank)
            ?.let(Locale::forLanguageTag)
            ?.takeIf { it.language.isNotBlank() }
        if (requestedLocale != null && languageAvailability(requestedLocale) >= TextToSpeech.LANG_AVAILABLE) {
            return BookSpeechLocaleSelection(requestedLocale, preserveSavedLanguage = true)
        }
        if (languageAvailability(deviceLocale) >= TextToSpeech.LANG_AVAILABLE) {
            return BookSpeechLocaleSelection(deviceLocale, preserveSavedLanguage = false)
        }
        return null
    }
}
