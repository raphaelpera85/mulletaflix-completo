package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BookSpeechPlaybackControllerTest {
    @Test
    fun `starts selected chunk only after speech engine is ready`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)

        controller.play(listOf("first", "selected", "last"), startIndex = 1)

        assertTrue(engine.initializeRequested)
        assertTrue(engine.spokenChunks.isEmpty())

        engine.listener.onReady()

        assertEquals(listOf("selected"), engine.spokenChunks)
    }

    @Test
    fun `advances through chunks and completes after final utterance`() {
        val engine = FakeBookSpeechEngine()
        val states = mutableListOf<BookSpeechState>()
        val controller = BookSpeechPlaybackController(engine, states::add)
        controller.play(listOf("one", "two"))
        engine.listener.onReady()

        assertEquals(listOf("one"), engine.spokenChunks)
        engine.listener.onUtteranceFinished("1")
        assertEquals(listOf("one", "two"), engine.spokenChunks)
        engine.listener.onUtteranceFinished("2")

        assertEquals(BookSpeechState.Completed, states.last())
    }

    @Test
    fun `stop cancels current speech and ignores late completion`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("one", "two"))
        engine.listener.onReady()

        controller.stop()
        engine.listener.onUtteranceFinished("1")

        assertTrue(engine.stopRequested)
        assertEquals(listOf("one"), engine.spokenChunks)
        assertFalse(controller.isSpeaking)
    }

    @Test
    fun `reports engine initialization failure without speaking`() {
        val engine = FakeBookSpeechEngine()
        val states = mutableListOf<BookSpeechState>()
        val controller = BookSpeechPlaybackController(engine, states::add)
        controller.play(listOf("one"))
        engine.listener.onError("Speech engine unavailable")

        assertEquals(BookSpeechState.Error("Speech engine unavailable"), states.last())
        assertTrue(engine.spokenChunks.isEmpty())
    }

    private class FakeBookSpeechEngine : BookSpeechEngine {
        lateinit var listener: BookSpeechEngine.Listener
        var initializeRequested = false
        var stopRequested = false
        val spokenChunks = mutableListOf<String>()

        override fun initialize(listener: BookSpeechEngine.Listener) {
            initializeRequested = true
            this.listener = listener
        }

        override fun speak(text: String, utteranceId: String): Boolean {
            spokenChunks += text
            return true
        }

        override fun stop() {
            stopRequested = true
        }

        override fun shutdown() = Unit
    }
}
