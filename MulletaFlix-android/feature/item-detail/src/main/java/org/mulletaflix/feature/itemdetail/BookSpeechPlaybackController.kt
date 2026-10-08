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

internal interface BookSpeechEngine {
    interface Listener {
        fun onReady()
        fun onUtteranceFinished(utteranceId: String)
        fun onError(message: String, utteranceId: String? = null)
    }

    fun initialize(listener: Listener)
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
        if (!engine.setSpeechRate(normalized / 100f)) {
            fail("Não foi possível ajustar a velocidade da narração.")
        }
    }

    fun shutdown() {
        stop()
        engine.shutdown()
        initialized = false
        initializationRequested = false
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
}
