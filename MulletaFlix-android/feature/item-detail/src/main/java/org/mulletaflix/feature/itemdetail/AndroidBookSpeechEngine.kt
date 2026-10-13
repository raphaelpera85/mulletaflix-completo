package org.mulletaflix.feature.itemdetail

import android.content.Context
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import android.speech.tts.Voice
import java.util.Locale

internal class AndroidBookSpeechEngine(context: Context) : BookSpeechEngine {
    private val appContext = context.applicationContext
    private var textToSpeech: TextToSpeech? = null
    private var listener: BookSpeechEngine.Listener? = null
    private var initializationPending = false
    private var initializationGeneration = 0
    private var speechRate = 1f
    private var speechLanguageTag: String? = null
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
            val selection = BookSpeechLocalePolicy.resolve(
                requestedLanguageTag = speechLanguageTag,
                deviceLocale = Locale.getDefault(),
                languageAvailability = engine::isLanguageAvailable,
            )
            if (selection == null || engine.setLanguage(selection.locale) < TextToSpeech.LANG_AVAILABLE) {
                engine.shutdown()
                textToSpeech = null
                dispatchError(listener, "O idioma do dispositivo não é compatível com o mecanismo de voz instalado.")
                return@TextToSpeech
            }
            if (!selection.preserveSavedLanguage) speechLanguageTag = null
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

    override fun availableLanguages(): List<BookSpeechLanguageOption> =
        textToSpeech?.availableLanguages.orEmpty()
            .filter { it.language.isNotBlank() }
            .distinctBy(Locale::toLanguageTag)
            .map { locale ->
                BookSpeechLanguageOption(
                    languageTag = locale.toLanguageTag(),
                    displayName = locale.getDisplayName(locale),
                )
            }
            .sortedBy { it.displayName.lowercase(Locale.getDefault()) }

    override fun availableVoices(languageTag: String?): List<BookSpeechVoiceOption> {
        val selectedLocale = languageTag?.let(Locale::forLanguageTag)
            ?: speechLanguageTag?.let(Locale::forLanguageTag)
            ?: Locale.getDefault()
        return textToSpeech?.voices.orEmpty()
            .filter { voice ->
                voice.locale.language == selectedLocale.language &&
                    (selectedLocale.country.isBlank() || voice.locale.country == selectedLocale.country)
            }
            .distinctBy(Voice::getName)
            .map { voice ->
                BookSpeechVoiceOption(
                    name = voice.name,
                    languageTag = voice.locale.toLanguageTag(),
                    displayName = buildString {
                        append(voice.locale.getDisplayName(voice.locale))
                        val variant = voice.name.substringAfter('#', "").replace('_', ' ').replace('-', ' ').trim()
                        if (variant.isNotEmpty()) append(" · ").append(variant)
                    },
                    quality = voice.quality,
                    networkRequired = voice.isNetworkConnectionRequired,
                )
            }
            .sortedWith(
                compareBy<BookSpeechVoiceOption> { it.networkRequired }
                    .thenByDescending { it.quality }
                    .thenBy { it.displayName.lowercase(Locale.getDefault()) }
                    .thenBy { it.name },
            )
    }

    override fun setSpeechLanguage(languageTag: String?): Boolean {
        val locale = languageTag?.let(Locale::forLanguageTag) ?: Locale.getDefault()
        if (languageTag != null && locale.language.isBlank()) return false
        val engine = textToSpeech
        if (engine != null && engine.setLanguage(locale) < TextToSpeech.LANG_AVAILABLE) return false
        speechLanguageTag = languageTag
        return true
    }

    override fun setSpeechVoice(voiceName: String?): Boolean {
        val engine = textToSpeech ?: return false
        if (voiceName == null) {
            val locale = speechLanguageTag?.let(Locale::forLanguageTag) ?: Locale.getDefault()
            return engine.setLanguage(locale) >= TextToSpeech.LANG_AVAILABLE
        }
        val voice = engine.voices.orEmpty().firstOrNull { it.name == voiceName } ?: return false
        val selectedLocale = speechLanguageTag?.let(Locale::forLanguageTag) ?: Locale.getDefault()
        if (voice.locale.language != selectedLocale.language ||
            (selectedLocale.country.isNotBlank() && voice.locale.country != selectedLocale.country)
        ) return false
        return engine.setVoice(voice) == TextToSpeech.SUCCESS
    }

    override fun setSpeechRate(rate: Float): Boolean {
        if (!rate.isFinite() || rate <= 0f) return false
        speechRate = rate
        val engine = textToSpeech ?: return true
        return engine.setSpeechRate(rate) == TextToSpeech.SUCCESS
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
