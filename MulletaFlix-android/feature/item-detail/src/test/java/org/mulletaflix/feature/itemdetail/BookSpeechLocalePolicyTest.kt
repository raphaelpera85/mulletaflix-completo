package org.mulletaflix.feature.itemdetail

import android.speech.tts.TextToSpeech
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class BookSpeechLocalePolicyTest {
    @Test
    fun `uses saved locale when installed`() {
        val selection = BookSpeechLocalePolicy.resolve("pt-BR", Locale.US) { locale ->
            if (locale == Locale.forLanguageTag("pt-BR")) TextToSpeech.LANG_COUNTRY_AVAILABLE
            else TextToSpeech.LANG_NOT_SUPPORTED
        }

        assertEquals(Locale.forLanguageTag("pt-BR"), selection?.locale)
        assertTrue(selection?.preserveSavedLanguage == true)
    }

    @Test
    fun `falls back to device locale when saved locale is unavailable`() {
        val selection = BookSpeechLocalePolicy.resolve("it-IT", Locale.US) { locale ->
            if (locale == Locale.US) TextToSpeech.LANG_COUNTRY_VAR_AVAILABLE
            else TextToSpeech.LANG_MISSING_DATA
        }

        assertEquals(Locale.US, selection?.locale)
        assertFalse(selection?.preserveSavedLanguage ?: true)
    }

    @Test
    fun `falls back to device locale when saved tag is malformed`() {
        val selection = BookSpeechLocalePolicy.resolve("???", Locale.CANADA) { locale ->
            if (locale == Locale.CANADA) TextToSpeech.LANG_AVAILABLE
            else TextToSpeech.LANG_NOT_SUPPORTED
        }

        assertEquals(Locale.CANADA, selection?.locale)
        assertFalse(selection?.preserveSavedLanguage ?: true)
    }

    @Test
    fun `returns unavailable when neither saved nor device locale is supported`() {
        val selection = BookSpeechLocalePolicy.resolve("it-IT", Locale.US) {
            TextToSpeech.LANG_NOT_SUPPORTED
        }

        assertNull(selection)
    }
}
