package org.mulletaflix.feature.itemdetail

import android.content.Context
import android.os.Bundle
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import java.util.Locale

internal class AndroidBookSpeechEngine(context: Context) : BookSpeechEngine {
    private val appContext = context.applicationContext
    private var textToSpeech: TextToSpeech? = null
    private var listener: BookSpeechEngine.Listener? = null

    override fun initialize(listener: BookSpeechEngine.Listener) {
        this.listener = listener
        if (textToSpeech != null) return
        textToSpeech = TextToSpeech(appContext) { status ->
            val engine = textToSpeech
            if (status != TextToSpeech.SUCCESS || engine == null) {
                listener.onError("O mecanismo de voz não está disponível.")
                return@TextToSpeech
            }
            val languageStatus = engine.setLanguage(Locale.getDefault())
            if (languageStatus == TextToSpeech.LANG_MISSING_DATA || languageStatus == TextToSpeech.LANG_NOT_SUPPORTED) {
                listener.onError("O idioma do dispositivo não é compatível com o mecanismo de voz instalado.")
                return@TextToSpeech
            }
            engine.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
                override fun onStart(utteranceId: String?) = Unit
                override fun onDone(utteranceId: String?) {
                    utteranceId?.let { this@AndroidBookSpeechEngine.listener?.onUtteranceFinished(it) }
                }
                @Deprecated("Deprecated in Java")
                override fun onError(utteranceId: String?) {
                    this@AndroidBookSpeechEngine.listener?.onError("Falha ao sintetizar a fala.")
                }
                override fun onError(utteranceId: String?, errorCode: Int) {
                    this@AndroidBookSpeechEngine.listener?.onError("Falha ao sintetizar a fala ($errorCode).")
                }
            })
            listener.onReady()
        }
    }

    override fun speak(text: String, utteranceId: String): Boolean {
        val params = Bundle().apply { putFloat(TextToSpeech.Engine.KEY_PARAM_VOLUME, 1f) }
        return textToSpeech?.speak(text, TextToSpeech.QUEUE_FLUSH, params, utteranceId) == TextToSpeech.SUCCESS
    }

    override fun stop() { textToSpeech?.stop() }

    override fun shutdown() {
        textToSpeech?.shutdown()
        textToSpeech = null
        listener = null
    }
}
