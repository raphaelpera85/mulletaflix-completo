package org.mulletaflix.feature.itemdetail

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
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
        fun onError(message: String)
    }

    fun initialize(listener: Listener)
    fun speak(text: String, utteranceId: String): Boolean
    fun stop()
    fun shutdown()
}

internal class BookSpeechPlaybackController(
    private val engine: BookSpeechEngine,
    private val onStateChanged: (BookSpeechState) -> Unit = {},
) : BookSpeechEngine.Listener {
    var state: BookSpeechState by mutableStateOf(BookSpeechState.Idle)
        private set
    var currentChunkIndex: Int = 0
        private set
    val isSpeaking: Boolean get() = state == BookSpeechState.Preparing || state == BookSpeechState.Speaking

    private var chunks: List<String> = emptyList()
    private var generation = 0
    private var activeUtteranceId: String? = null
    private var initialized = false

    fun play(chunks: List<String>, startIndex: Int = 0) {
        if (chunks.isEmpty()) return fail("Este trecho não contém texto para leitura.")
        generation++
        this.chunks = chunks
        currentChunkIndex = startIndex.coerceIn(chunks.indices)
        activeUtteranceId = null
        updateState(BookSpeechState.Preparing)
        if (initialized) speakCurrentChunk() else engine.initialize(this)
    }

    override fun onReady() {
        initialized = true
        if (state == BookSpeechState.Preparing) speakCurrentChunk()
    }

    override fun onUtteranceFinished(utteranceId: String) {
        if (state != BookSpeechState.Speaking || utteranceId != activeUtteranceId) return
        activeUtteranceId = null
        if (currentChunkIndex >= chunks.lastIndex) {
            updateState(BookSpeechState.Completed)
        } else {
            currentChunkIndex++
            speakCurrentChunk()
        }
    }

    override fun onError(message: String) {
        if (state == BookSpeechState.Preparing || state == BookSpeechState.Speaking) fail(message)
    }

    fun stop() {
        generation++
        activeUtteranceId = null
        chunks = emptyList()
        engine.stop()
        updateState(BookSpeechState.Idle)
    }

    fun shutdown() {
        stop()
        engine.shutdown()
        initialized = false
    }

    private fun speakCurrentChunk() {
        val utteranceId = "$generation:$currentChunkIndex"
        activeUtteranceId = utteranceId
        updateState(BookSpeechState.Speaking)
        if (!engine.speak(chunks[currentChunkIndex], utteranceId)) {
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
