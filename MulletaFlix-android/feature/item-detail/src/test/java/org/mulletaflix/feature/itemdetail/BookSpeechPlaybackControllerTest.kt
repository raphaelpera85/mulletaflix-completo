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
        engine.listener.onUtteranceFinished(engine.utteranceIds[0])
        assertEquals(listOf("one", "two"), engine.spokenChunks)
        engine.listener.onUtteranceFinished(engine.utteranceIds[1])

        assertEquals(BookSpeechState.Completed, states.last())
    }

    @Test
    fun `stop cancels current speech and ignores late completion`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("one", "two"))
        engine.listener.onReady()
        val utteranceId = engine.utteranceIds.single()

        controller.stop()
        engine.listener.onUtteranceFinished(utteranceId)

        assertTrue(engine.stopCount > 0)
        assertEquals(listOf("one"), engine.spokenChunks)
        assertFalse(controller.isSpeaking)
    }

    @Test
    fun `completion from previous playback does not advance the restarted book`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("old first", "old second"))
        engine.listener.onReady()
        val oldUtteranceId = engine.utteranceIds.last()

        controller.play(listOf("new book first", "new book second"))
        val newUtteranceId = engine.utteranceIds.last()
        engine.listener.onUtteranceFinished(oldUtteranceId)

        assertEquals("new book first", engine.spokenChunks.last())
        assertEquals(BookSpeechState.Speaking, controller.state)
        engine.listener.onUtteranceFinished(newUtteranceId)
        assertEquals("new book second", engine.spokenChunks.last())
    }

    @Test
    fun `reports each selected chunk so the reader can keep the page in sync`() {
        val engine = FakeBookSpeechEngine()
        val selectedChunks = mutableListOf<Int>()
        val controller = BookSpeechPlaybackController(engine, onChunkChanged = selectedChunks::add)
        controller.play(listOf("one", "two", "three"), startIndex = 1)
        engine.listener.onReady()

        engine.listener.onUtteranceFinished(engine.utteranceIds.last())

        assertEquals(listOf(1, 2), selectedChunks)
    }

    @Test
    fun `late error from previous chunk does not interrupt the current utterance`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("one", "two"))
        engine.listener.onReady()
        val firstUtteranceId = engine.utteranceIds.last()
        engine.listener.onUtteranceFinished(firstUtteranceId)

        engine.listener.onError("Late failure", firstUtteranceId)

        assertEquals(BookSpeechState.Speaking, controller.state)
        assertEquals(0, engine.stopCount)
        engine.listener.onUtteranceFinished(engine.utteranceIds.last())
        assertEquals(BookSpeechState.Completed, controller.state)
    }

    @Test
    fun `repeated play while engine initializes speaks only the latest selection`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("old selection"))
        controller.play(listOf("latest selection"))

        assertEquals(1, engine.initializeCount)
        engine.listener.onReady()
        assertEquals(listOf("latest selection"), engine.spokenChunks)
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

    @Test
    fun `late initialization failure after stop permits a new playback attempt`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("cancelled"))
        controller.stop()

        engine.listener.onError("Speech engine unavailable")
        controller.play(listOf("retry"))

        assertEquals(2, engine.initializeCount)
        engine.listener.onReady()
        assertEquals(listOf("retry"), engine.spokenChunks)
    }

    private class FakeBookSpeechEngine : BookSpeechEngine {
        lateinit var listener: BookSpeechEngine.Listener
        var initializeRequested = false
        var initializeCount = 0
        var stopCount = 0
        val spokenChunks = mutableListOf<String>()
        val utteranceIds = mutableListOf<String>()

        override fun initialize(listener: BookSpeechEngine.Listener) {
            initializeRequested = true
            initializeCount++
            this.listener = listener
        }

        override fun speak(text: String, utteranceId: String): Boolean {
            spokenChunks += text
            utteranceIds += utteranceId
            return true
        }

        override fun stop() {
            stopCount++
        }

        override fun shutdown() = Unit
    }
}
