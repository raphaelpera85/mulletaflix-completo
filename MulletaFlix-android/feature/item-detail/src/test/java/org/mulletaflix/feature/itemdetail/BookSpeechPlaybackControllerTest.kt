package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BookSpeechPlaybackControllerTest {
    @Test
    fun `applies saved voice after engine initialization and before first utterance`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)

        assertTrue(controller.setSpeechVoice("pt-br-female-local"))
        controller.play(listOf("olá"))
        engine.listener.onReady()

        assertEquals(listOf("pt-br-female-local"), engine.selectedVoices)
        assertEquals(listOf("olá"), engine.spokenChunks)
    }

    @Test
    fun `voice options initialize speech engine only when requested`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        var result: Result<List<BookSpeechVoiceOption>>? = null

        controller.loadAvailableVoices("pt-BR") { result = it }

        assertEquals(1, engine.initializeCount)
        assertTrue(engine.spokenChunks.isEmpty())
        engine.listener.onReady()

        assertEquals(engine.voices.filter { it.languageTag == "pt-BR" }, result?.getOrNull())
        assertTrue(engine.spokenChunks.isEmpty())
    }

    @Test
    fun `removed saved voice falls back and clears the stale preference`() {
        val engine = FakeBookSpeechEngine().apply {
            voices = voices.filterNot { it.name == "pt-br-female-local" }
        }
        var unavailableVoiceCleared = false
        val controller = BookSpeechPlaybackController(
            engine = engine,
            onSpeechVoiceUnavailable = { unavailableVoiceCleared = true },
        )
        assertTrue(controller.setSpeechVoice("pt-br-female-local"))

        controller.play(listOf("olá"))
        engine.listener.onReady()

        assertEquals(listOf(null), engine.selectedVoices)
        assertTrue(unavailableVoiceCleared)
        assertEquals(listOf("olá"), engine.spokenChunks)
    }

    @Test
    fun `changing voice during speech restarts current chunk and ignores stale completion`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("first", "second"))
        engine.listener.onReady()
        val oldUtterance = engine.utteranceIds.last()

        assertTrue(controller.setSpeechVoice("pt-br-female-local"))
        engine.listener.onUtteranceFinished(oldUtterance)

        assertEquals(listOf("first", "first"), engine.spokenChunks)
        assertEquals(BookSpeechState.Speaking, controller.state)
        assertEquals(listOf("pt-br-female-local"), engine.selectedVoices)
        engine.listener.onUtteranceFinished(engine.utteranceIds.last())
        assertEquals("second", engine.spokenChunks.last())
    }

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
    fun `selected speech rate is applied before speech and retained for following chunks`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)

        controller.play(listOf("first", "second"))
        controller.setSpeechRatePercent(125)
        assertTrue("Rate changes during engine initialization must be deferred", engine.speechRates.isEmpty())
        engine.listener.onReady()
        engine.listener.onUtteranceFinished(engine.utteranceIds.last())

        assertEquals(listOf(1.25f, 1.25f), engine.speechRates)
        assertEquals(listOf("first", "second"), engine.spokenChunks)
    }

    @Test
    fun `language options initialize speech engine only when requested`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        var result: Result<List<BookSpeechLanguageOption>>? = null

        controller.loadAvailableLanguages { result = it }

        assertEquals(1, engine.initializeCount)
        assertTrue(engine.spokenChunks.isEmpty())
        engine.listener.onReady()

        assertEquals(engine.languages, result?.getOrNull())
        assertTrue(engine.spokenChunks.isEmpty())
    }

    @Test
    fun `selected language is applied before speech starts`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        assertTrue(controller.setSpeechLanguage("pt-BR"))

        controller.play(listOf("olá"))
        engine.listener.onReady()

        assertEquals(listOf("pt-BR"), engine.selectedLanguages)
        assertEquals(listOf("olá"), engine.spokenChunks)
    }

    @Test
    fun `unavailable saved language falls back to device default and keeps selector available`() {
        val engine = FakeBookSpeechEngine().apply {
            languages = listOf(BookSpeechLanguageOption("en-US", "English (United States)"))
        }
        var unavailableLanguageCleared = false
        val controller = BookSpeechPlaybackController(
            engine = engine,
            onSpeechLanguageUnavailable = { unavailableLanguageCleared = true },
        )
        assertTrue(controller.setSpeechLanguage("pt-BR"))
        var result: Result<List<BookSpeechLanguageOption>>? = null

        controller.loadAvailableLanguages { result = it }
        engine.listener.onReady()

        assertEquals(engine.languages, result?.getOrNull())
        assertEquals(listOf(null), engine.selectedLanguages)
        assertTrue(unavailableLanguageCleared)
    }

    @Test
    fun `changing language during speech restarts current chunk and ignores stale completion`() {
        val engine = FakeBookSpeechEngine()
        val controller = BookSpeechPlaybackController(engine)
        controller.play(listOf("one", "two"))
        engine.listener.onReady()
        val oldUtterance = engine.utteranceIds.single()

        assertTrue(controller.setSpeechLanguage("en-US"))
        engine.listener.onUtteranceFinished(oldUtterance)

        assertEquals(listOf("one", "one"), engine.spokenChunks)
        assertEquals(BookSpeechState.Speaking, controller.state)
        assertEquals(listOf("en-US"), engine.selectedLanguages)
        engine.listener.onUtteranceFinished(engine.utteranceIds.last())
        assertEquals("two", engine.spokenChunks.last())
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
        val speechRates = mutableListOf<Float>()
        val spokenChunks = mutableListOf<String>()
        val utteranceIds = mutableListOf<String>()
        var languages = listOf(
            BookSpeechLanguageOption("en-US", "English (United States)"),
            BookSpeechLanguageOption("pt-BR", "Português (Brasil)"),
        )
        val selectedLanguages = mutableListOf<String?>()
        var voices = listOf(
            BookSpeechVoiceOption("pt-br-female-local", "pt-BR", "Português feminino", quality = 400, networkRequired = false),
            BookSpeechVoiceOption("pt-br-male-online", "pt-BR", "Português masculino", quality = 500, networkRequired = true),
            BookSpeechVoiceOption("en-us-default", "en-US", "English", quality = 500, networkRequired = false),
        )
        val selectedVoices = mutableListOf<String?>()

        override fun initialize(listener: BookSpeechEngine.Listener) {
            initializeRequested = true
            initializeCount++
            this.listener = listener
        }

        override fun availableLanguages(): List<BookSpeechLanguageOption> = languages

        override fun availableVoices(languageTag: String?): List<BookSpeechVoiceOption> =
            if (languageTag == null) voices else voices.filter { it.languageTag == languageTag }

        override fun setSpeechVoice(voiceName: String?): Boolean {
            if (voiceName != null && voices.none { it.name == voiceName }) return false
            selectedVoices += voiceName
            return true
        }

        override fun setSpeechLanguage(languageTag: String?): Boolean {
            if (languageTag != null && languages.none { it.languageTag == languageTag }) return false
            selectedLanguages += languageTag
            return true
        }

        override fun setSpeechRate(rate: Float): Boolean {
            speechRates += rate
            return true
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
