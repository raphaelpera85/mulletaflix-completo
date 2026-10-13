package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.runtime.setValue

internal sealed interface BookSpeechState {
    data object Idle : BookSpeechState
    data object Preparing : BookSpeechState
    data object Speaking : BookSpeechState
    data object Completed : BookSpeechState
    data class Error(val message: String) : BookSpeechState
}

internal data class BookSpeechLanguageOption(
    val languageTag: String,
    val displayName: String,
)

internal data class BookSpeechVoiceOption(
    val name: String,
    val languageTag: String,
    val displayName: String,
    val quality: Int,
    val networkRequired: Boolean,
)

internal interface BookSpeechEngine {
    interface Listener {
        fun onReady()
        fun onUtteranceFinished(utteranceId: String)
        fun onError(message: String, utteranceId: String? = null)
    }

    fun initialize(listener: Listener)
    fun availableLanguages(): List<BookSpeechLanguageOption>
    fun availableVoices(languageTag: String?): List<BookSpeechVoiceOption> = emptyList()
    fun setSpeechLanguage(languageTag: String?): Boolean
    fun setSpeechVoice(voiceName: String?): Boolean = voiceName == null
    fun setSpeechRate(rate: Float): Boolean
    fun speak(text: String, utteranceId: String): Boolean
    fun stop()
    fun shutdown()
}

internal val LocalBookSpeechEngineFactory =
    staticCompositionLocalOf<(Context) -> BookSpeechEngine> { { context -> AndroidBookSpeechEngine(context) } }

internal class BookSpeechPlaybackController(
    private val engine: BookSpeechEngine,
    private val onStateChanged: (BookSpeechState) -> Unit = {},
    private val onChunkChanged: (Int) -> Unit = {},
    private val onSpeechLanguageUnavailable: () -> Unit = {},
    private val onSpeechVoiceUnavailable: () -> Unit = {},
) : BookSpeechEngine.Listener {
    var state: BookSpeechState by mutableStateOf(BookSpeechState.Idle)
        private set
    var currentChunkIndex: Int = 0
        private set
    var speechRatePercent: Int = BookSpeechRate.DEFAULT_PERCENT
        private set
    val isSpeaking: Boolean get() = state == BookSpeechState.Preparing || state == BookSpeechState.Speaking

    private var chunks: List<String> = emptyList()
    private var generation = 0
    private var activeUtteranceId: String? = null
    private var initialized = false
    private var initializationRequested = false
    private var selectedLanguageTag: String? = null
    private var selectedVoiceName: String? = null
    private val languageRequests = mutableListOf<(Result<List<BookSpeechLanguageOption>>) -> Unit>()
    private val voiceRequests = mutableListOf<Pair<String?, (Result<List<BookSpeechVoiceOption>>) -> Unit>>()

    fun play(chunks: List<String>, startIndex: Int = 0) {
        if (chunks.isEmpty()) return fail("Este trecho não contém texto para leitura.")
        generation++
        this.chunks = chunks
        currentChunkIndex = startIndex.coerceIn(chunks.indices)
        onChunkChanged(currentChunkIndex)
        activeUtteranceId = null
        updateState(BookSpeechState.Preparing)
        if (initialized) {
            speakCurrentChunk()
        } else if (!initializationRequested) {
            initializationRequested = true
            engine.initialize(this)
        }
    }

    override fun onReady() {
        initialized = true
        initializationRequested = false
        val selectedLanguage = selectedLanguageTag
        if (selectedLanguage != null && !engine.setSpeechLanguage(selectedLanguage)) {
            if (!engine.setSpeechLanguage(null)) {
                val message = "O idioma selecionado não está disponível e o padrão do dispositivo falhou."
                completeLanguageRequests(Result.failure(IllegalStateException(message)))
                completeVoiceRequests(Result.failure(IllegalStateException(message)))
                onError(message)
                return
            }
            selectedLanguageTag = null
            onSpeechLanguageUnavailable()
        }
        val voiceName = selectedVoiceName
        if (voiceName != null && !engine.setSpeechVoice(voiceName)) {
            engine.setSpeechVoice(null)
            selectedVoiceName = null
            onSpeechVoiceUnavailable()
        }
        completeLanguageRequests(Result.success(engine.availableLanguages()))
        completeVoiceRequests()
        if (state == BookSpeechState.Preparing) speakCurrentChunk()
    }

    override fun onUtteranceFinished(utteranceId: String) {
        if (state != BookSpeechState.Speaking || utteranceId != activeUtteranceId) return
        activeUtteranceId = null
        if (currentChunkIndex >= chunks.lastIndex) {
            updateState(BookSpeechState.Completed)
        } else {
            currentChunkIndex++
            onChunkChanged(currentChunkIndex)
            speakCurrentChunk()
        }
    }

    override fun onError(message: String, utteranceId: String?) {
        if (!initialized && initializationRequested && utteranceId == null) {
            initializationRequested = false
        }
        val isActiveError = when (state) {
            BookSpeechState.Preparing -> utteranceId == null
            BookSpeechState.Speaking -> utteranceId == null || utteranceId == activeUtteranceId
            else -> false
        }
        if (isActiveError) {
            fail(message)
        }
        if (!initialized && utteranceId == null) {
            completeLanguageRequests(Result.failure(IllegalStateException(message)))
            completeVoiceRequests(Result.failure(IllegalStateException(message)))
        }
    }

    fun loadAvailableLanguages(callback: (Result<List<BookSpeechLanguageOption>>) -> Unit) {
        if (initialized) {
            callback(Result.success(engine.availableLanguages()))
            return
        }
        languageRequests += callback
        if (initializationRequested) return
        initializationRequested = true
        engine.initialize(this)
    }

    fun setSpeechLanguage(languageTag: String?): Boolean {
        if (languageTag != null && languageTag.isBlank()) return false
        if (initialized && !engine.setSpeechLanguage(languageTag)) return false
        selectedLanguageTag = languageTag
        val voiceName = selectedVoiceName
        if (initialized && voiceName != null && !engine.setSpeechVoice(voiceName)) {
            engine.setSpeechVoice(null)
            selectedVoiceName = null
            onSpeechVoiceUnavailable()
        }
        if (initialized && state == BookSpeechState.Speaking) {
            generation++
            activeUtteranceId = null
            engine.stop()
            speakCurrentChunk()
        }
        return true
    }

    fun loadAvailableVoices(
        languageTag: String?,
        callback: (Result<List<BookSpeechVoiceOption>>) -> Unit,
    ) {
        if (initialized) {
            callback(Result.success(engine.availableVoices(languageTag)))
            return
        }
        voiceRequests += languageTag to callback
        if (initializationRequested) return
        initializationRequested = true
        engine.initialize(this)
    }

    fun setSpeechVoice(voiceName: String?): Boolean {
        if (voiceName != null && (voiceName.isBlank() || voiceName.length > 256)) return false
        if (initialized && !engine.setSpeechVoice(voiceName)) return false
        selectedVoiceName = voiceName
        if (initialized && state == BookSpeechState.Speaking) {
            generation++
            activeUtteranceId = null
            engine.stop()
            speakCurrentChunk()
        }
        return true
    }

    fun stop() {
        generation++
        activeUtteranceId = null
        chunks = emptyList()
        engine.stop()
        updateState(BookSpeechState.Idle)
    }

    fun setSpeechRatePercent(percent: Int) {
        val normalized = BookSpeechRate.normalize(percent)
        if (speechRatePercent == normalized) return
        speechRatePercent = normalized
        if (!initialized) return
        if (!engine.setSpeechRate(normalized / 100f)) {
            fail("Não foi possível ajustar a velocidade da narração.")
        }
    }

    fun shutdown() {
        stop()
        engine.shutdown()
        initialized = false
        initializationRequested = false
        languageRequests.clear()
        voiceRequests.clear()
    }

    private fun speakCurrentChunk() {
        val utteranceId = "$generation:$currentChunkIndex"
        activeUtteranceId = utteranceId
        updateState(BookSpeechState.Speaking)
        if (!engine.setSpeechRate(speechRatePercent / 100f) ||
            !engine.speak(chunks[currentChunkIndex], utteranceId)
        ) {
            fail("Não foi possível iniciar a leitura em voz alta.")
        }
    }

    private fun fail(message: String) {
        activeUtteranceId = null
        engine.stop()
        updateState(BookSpeechState.Error(message))
    }

    private fun updateState(value: BookSpeechState) {
        state = value
        onStateChanged(value)
    }

    private fun completeLanguageRequests(result: Result<List<BookSpeechLanguageOption>>) {
        languageRequests.toList().also { languageRequests.clear() }.forEach { it(result) }
    }

    private fun completeVoiceRequests() {
        completeVoiceRequests(null)
    }

    private fun completeVoiceRequests(failure: Result<List<BookSpeechVoiceOption>>?) {
        voiceRequests.toList().also { voiceRequests.clear() }.forEach { (languageTag, callback) ->
            callback(failure ?: Result.success(engine.availableVoices(languageTag)))
        }
    }
}
