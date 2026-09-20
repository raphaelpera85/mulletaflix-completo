package org.mulletaflix.feature.search

import android.speech.SpeechRecognizer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class VoiceSearchPolicyTest {
    @Test
    fun `first non blank recognition result becomes the query`() {
        assertEquals("Duna Parte Dois", recognizedVoiceQuery(listOf("  ", " Duna Parte Dois ")))
        assertNull(recognizedVoiceQuery(listOf(" ", "")))
        assertNull(recognizedVoiceQuery(null))
    }

    @Test
    fun `recognition errors become actionable messages`() {
        assertEquals("Não entendi a busca. Tente falar novamente.", voiceSearchErrorMessage(SpeechRecognizer.ERROR_NO_MATCH))
        assertEquals("Não foi possível acessar o microfone.", voiceSearchErrorMessage(SpeechRecognizer.ERROR_AUDIO))
    }
}
