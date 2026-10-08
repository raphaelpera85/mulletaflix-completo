package org.mulletaflix.feature.itemdetail

import android.content.Context
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import java.util.Locale

internal class AndroidBookSpeechEngine(context: Context) : BookSpeechEngine {
    private val appContext = context.applicationContext
    private var textToSpeech: TextToSpeech? = null
    private var listener: BookSpeechEngine.Listener? = null
    private var initializationPending = false
    private var initializationGeneration = 0
    private val mainHandler = Handler(Looper.getMainLooper())

    override fun initialize(listener: BookSpeechEngine.Listener) {
        this.listener = listener
        if (textToSpeech != null || initializationPending) return
        initializationPending = true
        val generation = ++initializationGeneration
        textToSpeech = TextToSpeech(appContext) { status ->
            if (generation != initializationGeneration) return@TextToSpeech
            initializationPending = false
            val engine = textToSpeech
            if (status != TextToSpeech.SUCCESS || engine == null) {
                textToSpeech?.shutdown()
                textToSpeech = null
                dispatchError(listener, "O mecanismo de voz não está disponível.")
                return@TextToSpeech
            }
            val languageStatus = engine.setLanguage(Locale.getDefault())
            if (languageStatus == TextToSpeech.LANG_MISSING_DATA || languageStatus == TextToSpeech.LANG_NOT_SUPPORTED) {
                engine.shutdown()
                textToSpeech = null
                dispatchError(listener, "O idioma do dispositivo não é compatível com o mecanismo de voz instalado.")
                return@TextToSpeech
            }
            engine.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
                override fun onStart(utteranceId: String?) = Unit
                override fun onDone(utteranceId: String?) {
                    utteranceId?.let { id -> dispatch { this@AndroidBookSpeechEngine.listener?.onUtteranceFinished(id) } }
                }
                @Deprecated("Deprecated in Java")
                override fun onError(utteranceId: String?) {
                    this@AndroidBookSpeechEngine.listener?.let { target ->
                        dispatchError(target, "Falha ao sintetizar a fala.", utteranceId)
                    }
                }
                override fun onError(utteranceId: String?, errorCode: Int) {
                    this@AndroidBookSpeechEngine.listener?.let { target ->
                        dispatchError(target, "Falha ao sintetizar a fala ($errorCode).", utteranceId)
                    }
                }
            })
            dispatch { if (this@AndroidBookSpeechEngine.listener === listener) listener.onReady() }
        }
    }

    override fun speak(text: String, utteranceId: String): Boolean {
        val params = Bundle().apply { putFloat(TextToSpeech.Engine.KEY_PARAM_VOLUME, 1f) }
        return textToSpeech?.speak(text, TextToSpeech.QUEUE_FLUSH, params, utteranceId) == TextToSpeech.SUCCESS
    }

    override fun stop() { textToSpeech?.stop() }

    override fun shutdown() {
        initializationGeneration++
        initializationPending = false
        textToSpeech?.shutdown()
        textToSpeech = null
        listener = null
    }

    private fun dispatchError(
        target: BookSpeechEngine.Listener,
        message: String,
        utteranceId: String? = null,
    ) = dispatch { if (listener === target) target.onError(message, utteranceId) }

    private fun dispatch(action: () -> Unit) {
        if (Looper.myLooper() == Looper.getMainLooper()) action() else mainHandler.post(action)
    }
}
