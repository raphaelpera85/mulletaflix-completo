package org.mulletaflix.feature.search

import android.speech.SpeechRecognizer

internal fun recognizedVoiceQuery(results: List<String>?): String? =
    results.orEmpty().firstOrNull { it.isNotBlank() }?.trim()?.takeIf { it.isNotEmpty() }

internal fun voiceSearchErrorMessage(errorCode: Int): String = when (errorCode) {
    SpeechRecognizer.ERROR_AUDIO -> "Não foi possível acessar o microfone."
    SpeechRecognizer.ERROR_NETWORK,
    SpeechRecognizer.ERROR_NETWORK_TIMEOUT -> "Não foi possível conectar ao reconhecimento de voz."
    SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS -> "Permissão do microfone não concedida."
    SpeechRecognizer.ERROR_NO_MATCH -> "Não entendi a busca. Tente falar novamente."
    SpeechRecognizer.ERROR_RECOGNIZER_BUSY -> "O reconhecimento de voz está ocupado. Tente novamente."
    else -> "Não foi possível reconhecer a busca por voz."
}

