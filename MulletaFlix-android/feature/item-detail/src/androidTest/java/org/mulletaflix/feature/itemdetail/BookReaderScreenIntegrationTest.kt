package org.mulletaflix.feature.itemdetail

import android.graphics.Color
import android.graphics.Paint
import android.graphics.pdf.PdfDocument
import android.content.ContextWrapper
import android.os.Build
import android.os.ext.SdkExtensions
import android.view.WindowManager
import android.view.View
import android.view.ViewGroup
import android.webkit.WebView
import androidx.activity.ComponentActivity
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.assertHasNoClickAction
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.assert
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollToIndex
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.ViewModelStore
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.ByteArrayOutputStream
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import java.util.concurrent.CountDownLatch
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okio.Buffer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SavedServerSession
import org.mulletaflix.core.api.SessionRepository
import retrofit2.Retrofit

@RunWith(AndroidJUnit4::class)
class BookReaderScreenIntegrationTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    private val context = InstrumentationRegistry.getInstrumentation().targetContext
    private val sessionScope = HomeFeedCacheScope(
        serverId = "reader-screen-test-server",
        serverUrl = "http://localhost",
        userId = "reader-screen-test-user",
    )
    private lateinit var server: MockWebServer
    private val viewModelStore = ViewModelStore()

    @Before
    fun setUp() {
        runBlocking { BookReaderProgressStore(context).removeSpeechRatePercent(sessionScope) }
        server = MockWebServer().apply { start() }
    }

    @After
    fun tearDown() {
        composeRule.waitForIdle()
        viewModelStore.clear()
        if (::server.isInitialized) server.shutdown()
    }

    @Test
    fun retryAfterHttpFailureOpensEpubAndEnablesReaderControls() {
        val epub = createEpub()
        server.enqueue(MockResponse().setResponseCode(503))
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/epub+zip")
                .setBody(Buffer().write(epub)),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]

        composeRule.setContent {
            MaterialTheme {
                BookReaderScreen(itemId = "reader-screen-book", onBack = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching {
                composeRule.onNodeWithText(
                    "Não foi possível carregar o livro (HTTP 503).",
                    useUnmergedTree = true,
                ).assertIsDisplayed()
            }.isSuccess
        }
        val retryButton = composeRule.onNodeWithContentDescription("Tentar carregar o livro novamente")
        retryButton.assertIsDisplayed().performClick()

        composeRule.waitUntil(timeoutMillis = 30_000) {
            runCatching {
                composeRule.onNodeWithContentDescription("Aumentar tamanho do texto").assertIsEnabled()
        }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ouvir livro")
            .assertIsDisplayed()
            .assertIsEnabled()
            .assertHasClickAction()
            .performClick()
        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching {
                composeRule.onNodeWithContentDescription("Parar narração")
                    .assertIsDisplayed()
                    .assert(SemanticsMatcher.expectValue(SemanticsProperties.StateDescription, "Em reprodução"))
            }.isSuccess
        }
        assertEquals(2, composeRule.onAllNodesWithText("100%").fetchSemanticsNodes().size)
        composeRule.onNodeWithContentDescription("Aumentar velocidade da narração")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithText("125%").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Página anterior").assertIsEnabled()
        composeRule.onNodeWithContentDescription("Próxima página").assertIsEnabled()
        assertTrue(
            "O conteúdo do capítulo não apareceu no WebView do Readium.",
            awaitWebViewText("Conteúdo EPUB entregue pela fixture HTTP.")
                .contains("Conteúdo EPUB entregue pela fixture HTTP."),
        )

        composeRule.onNodeWithContentDescription("Abrir sumário").assertIsEnabled().performClick()
        composeRule.onNodeWithText("Parte I", useUnmergedTree = true)
            .assertIsDisplayed()
            .assert(isHeading())
            .assertHasNoClickAction()
        composeRule.onNodeWithText("Capítulo 2")
            .assertIsDisplayed()
            .assertHasClickAction()
            .assert(SemanticsMatcher.expectValue(SemanticsProperties.StateDescription, "Nível 2"))
            .performClick()
        assertTrue(
            "A seleção do sumário não navegou para o segundo capítulo.",
            awaitWebViewText("Conteúdo exclusivo do segundo capítulo.")
                .contains("Conteúdo exclusivo do segundo capítulo."),
        )
        composeRule.onNodeWithContentDescription("Ouvir livro").assertIsDisplayed()

        composeRule.onNodeWithContentDescription("Aumentar tamanho do texto").performClick()
        composeRule.onNodeWithText("110%").assertIsDisplayed()

        val firstRequest = server.takeRequest()
        val retryRequest = server.takeRequest()
        assertEquals("/BookReader/Items/reader-screen-book/BookReader/Epub", firstRequest.path)
        assertEquals(firstRequest.path, retryRequest.path)
    }

    @Test
    fun epubPaginationControlsMoveAndPersistTheReadingPosition() {
        val itemId = "reader-pagination-${System.nanoTime()}"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/epub+zip")
                .setBody(Buffer().write(createEpub())),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]
        val progressStore = BookReaderProgressStore(context)

        try {
            composeRule.setContent {
                MaterialTheme {
                    BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
                }
            }

            composeRule.waitUntil(timeoutMillis = 30_000) {
                runCatching {
                    composeRule.onNodeWithContentDescription("Próxima página").assertIsEnabled()
                    composeRule.onNodeWithContentDescription("Página anterior").assertIsEnabled()
                }.isSuccess
            }
            assertTrue(
                "O conteúdo inicial do EPUB deve estar renderizado no WebView.",
                awaitWebViewText("Leitura integrada funcionando").contains("Leitura integrada funcionando"),
            )
            composeRule.waitUntil(timeoutMillis = 10_000) {
                runBlocking { progressStore.read(sessionScope, itemId) } != null
            }
            val initialPosition = runBlocking { progressStore.read(sessionScope, itemId) }
                ?.toJSON()
                ?.toString()
            assertTrue("A posição inicial do EPUB deve ser armazenada.", initialPosition != null)

            composeRule.onNodeWithContentDescription("Próxima página").performClick()
            composeRule.waitUntil(timeoutMillis = 15_000) {
                val currentPosition = runBlocking { progressStore.read(sessionScope, itemId) }
                    ?.toJSON()
                    ?.toString()
                currentPosition != null && currentPosition != initialPosition
            }
            val nextPosition = runBlocking { progressStore.read(sessionScope, itemId) }
                ?.toJSON()
                ?.toString()
            assertTrue("Avançar página deve atualizar e persistir o localizador.", nextPosition != initialPosition)

            composeRule.onNodeWithContentDescription("Página anterior").performClick()
            composeRule.waitUntil(timeoutMillis = 15_000) {
                runBlocking { progressStore.read(sessionScope, itemId) }
                    ?.toJSON()
                    ?.toString() == initialPosition
            }
            assertEquals(
                "Voltar página deve restaurar e persistir a posição anterior.",
                initialPosition,
                runBlocking { progressStore.read(sessionScope, itemId) }?.toJSON()?.toString(),
            )
        } finally {
            runBlocking { progressStore.remove(sessionScope, itemId) }
        }
    }

    @Test
    fun readerKeepsScreenOnOnlyWhileVisibleAndRestoresPreviousWindowState() {
        server.enqueue(MockResponse().setResponseCode(503))
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]
        val activity = composeRule.activity
        val originalKeepScreenOn = activity.window.attributes.flags and
            WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON != 0
        val readerVisible = mutableStateOf(true)

        composeRule.setContent {
            CompositionLocalProvider(LocalContext provides ContextWrapper(activity)) {
                if (readerVisible.value) {
                    MaterialTheme {
                        BookReaderScreen(itemId = "keep-screen-reader", onBack = {}, viewModel = viewModel)
                    }
                }
            }
        }
        composeRule.waitForIdle()
        assertTrue(
            "O leitor deve impedir que a tela apague enquanto está visível.",
            activity.window.attributes.flags and WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON != 0,
        )

        composeRule.runOnIdle { readerVisible.value = false }
        composeRule.waitForIdle()
        assertEquals(
            "Ao sair do leitor, o estado prévio da janela deve ser restaurado.",
            originalKeepScreenOn,
            activity.window.attributes.flags and WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON != 0,
        )

        activity.runOnUiThread {
            activity.window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        }
        composeRule.runOnIdle { readerVisible.value = true }
        composeRule.waitForIdle()
        composeRule.runOnIdle { readerVisible.value = false }
        composeRule.waitForIdle()
        assertTrue(
            "Ao sair do leitor, o flag keep-screen preexistente deve continuar ativo.",
            activity.window.attributes.flags and WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON != 0,
        )
    }

    @Test
    fun pdfHttpPayloadUsesTheNativePaginatedReader() {
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/pdf")
                .setBody(Buffer().write(createPdfFixture())),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]

        viewModel.load("pdf-http-book")
        composeRule.waitUntil(timeoutMillis = 20_000) { !viewModel.state.value.isLoading }

        val state = viewModel.state.value
        assertNull(state.error)
        assertNull(state.publication)
        assertTrue(state.pageBook is PdfBookDocument)
        assertEquals(2, state.pageBook?.pageCount)
        assertEquals(
            "/BookReader/Items/pdf-http-book/BookReader/Epub",
            server.takeRequest(5, TimeUnit.SECONDS)?.path,
        )
    }

    @Test
    fun invalidCbrResponseShowsPortugueseRetryAndThenOpensEpub() {
        val itemId = "reader-screen-invalid-cbr-${System.nanoTime()}"
        val errorMessage = "O arquivo CBR está inválido ou não é compatível."
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/vnd.comicbook-rar")
                .setBody("not a RAR archive"),
        )
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/epub+zip")
                .setBody(Buffer().write(createEpub())),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]

        composeRule.setContent {
            MaterialTheme {
                BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText(errorMessage, useUnmergedTree = true).assertIsDisplayed() }
                .isSuccess
        }
        composeRule.onNodeWithContentDescription("Tentar carregar o livro novamente")
            .assertIsDisplayed()
            .performClick()

        composeRule.waitUntil(timeoutMillis = 30_000) {
            runCatching {
                composeRule.onNodeWithContentDescription("Aumentar tamanho do texto").assertIsEnabled()
            }.isSuccess
        }
        composeRule.onNodeWithText(errorMessage, useUnmergedTree = true).assertDoesNotExist()
        assertTrue(
            "O retry não exibiu o EPUB recebido depois do CBR inválido.",
            awaitWebViewText("Conteúdo EPUB entregue pela fixture HTTP.")
                .contains("Conteúdo EPUB entregue pela fixture HTTP."),
        )

        val firstRequest = server.takeRequest()
        val retryRequest = server.takeRequest()
        assertEquals("/BookReader/Items/$itemId/BookReader/Epub", firstRequest.path)
        assertEquals(firstRequest.path, retryRequest.path)
    }

    @Test
    fun directTextBookCanSaveAndRestoreBookmarkForCurrentChunk() {
        val itemId = "reader-screen-text-${System.nanoTime()}"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Capítulo inicial. " + "Conteúdo do livro. ".repeat(90)),
        )
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/json")
                .setBody("""{"status":"direct"}"""),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]

        composeRule.setContent {
            MaterialTheme {
                BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Próximo trecho").performClick()
        composeRule.onNodeWithText("Trecho 2 de 2").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Salvar posição atual").performClick()
        composeRule.onNodeWithText("Salvar").performClick()
        composeRule.waitUntil(timeoutMillis = 10_000) {
            runCatching { composeRule.onNodeWithText("Marcador salvo.").assertIsDisplayed() }.isSuccess
        }

        val savedBookmark = runBlocking {
            BookReaderProgressStore(context).readBookmarks(sessionScope, itemId)?.single()
        }
        assertEquals("mulletaflix-text-chunk-1", savedBookmark?.locator?.href?.toString())

        composeRule.onNodeWithContentDescription("Trecho anterior").performClick()
        composeRule.onNodeWithText("Trecho 1 de 2").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Marcadores salvos (1)").performClick()
        composeRule.onNodeWithText("Trecho 2").assertIsDisplayed().performClick()
        composeRule.onNodeWithText("Trecho 2 de 2").assertIsDisplayed()
    }

    @Test
    fun textBookSpeechAdvancesChunksAndStopsWhenReaderGoesToBackground() {
        val itemId = "reader-speech-${System.nanoTime()}"
        val scopedSession = sessionScope.copy(serverId = "reader-speech-server-${System.nanoTime()}")
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Capítulo inicial. " + "Conteúdo do livro. ".repeat(90)),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(scopedSession), context) as T
            },
        )[BookReaderViewModel::class.java]
        val speechEngine = FakeBookSpeechEngine()
        val readerItemId = mutableStateOf(itemId)
        val lifecycleOwner = SpeechTestLifecycleOwner()
        composeRule.runOnUiThread {
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
        }

        composeRule.setContent {
            CompositionLocalProvider(
                LocalLifecycleOwner provides lifecycleOwner,
                LocalBookSpeechEngineFactory provides { speechEngine },
            ) {
                MaterialTheme {
                    BookReaderScreen(itemId = readerItemId.value, onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler trecho em voz alta")
            .assertIsDisplayed()
            .performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.spokenChunks.size == 1 }
        assertTrue(speechEngine.spokenChunks.first().startsWith("Capítulo inicial."))
        composeRule.onNodeWithContentDescription("Parar leitura em voz alta").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Aumentar velocidade da narração")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithText("125%").assertIsDisplayed()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            speechEngine.speechRates.lastOrNull() == 1.25f
        }

        composeRule.runOnUiThread {
            speechEngine.listener.onUtteranceFinished(speechEngine.utteranceIds.first())
        }
        composeRule.waitUntil(timeoutMillis = 5_000) {
            speechEngine.spokenChunks.size == 2 &&
                runCatching { composeRule.onNodeWithText("Trecho 2 de 2").assertIsDisplayed() }.isSuccess
        }

        composeRule.runOnUiThread {
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
        }
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.stopCount == 1 }
        composeRule.onNodeWithContentDescription("Ler trecho em voz alta").assertIsDisplayed()
        assertEquals(0, speechEngine.shutdownCount)

        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Conteúdo após reabrir o leitor."),
        )
        composeRule.runOnUiThread {
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
            readerItemId.value = "${itemId}-next-book"
        }
        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 1").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithText("125%").assertIsDisplayed()

        composeRule.runOnUiThread {
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_DESTROY)
        }
    }

    @Test
    fun speechStopsBeforeJumpingToAnotherChapterFromTheContents() {
        val itemId = "reader-speech-toc-${System.nanoTime()}"
        val fictionBook = """
            <FictionBook><body>
              <section><title><p>Capítulo 1</p></title><p>${"Texto inicial. ".repeat(100)}</p></section>
              <section><title><p>Capítulo 2</p></title><p>Texto do segundo capítulo.</p></section>
            </body></FictionBook>
        """.trimIndent()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/x-fictionbook+xml")
                .setBody(fictionBook),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]
        val speechEngine = FakeBookSpeechEngine()

        composeRule.setContent {
            CompositionLocalProvider(LocalBookSpeechEngineFactory provides { speechEngine }) {
                MaterialTheme {
                    BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithContentDescription("Abrir sumário").assertIsEnabled() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler trecho em voz alta").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.spokenChunks.size == 1 }

        composeRule.onNodeWithContentDescription("Abrir sumário").performClick()
        composeRule.onNodeWithText("Capítulo 2").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            speechEngine.stopCount > 0 &&
                runCatching { composeRule.onNodeWithText("Capítulo 2", substring = true).assertIsDisplayed() }.isSuccess
        }
        assertEquals(1, speechEngine.stopCount)
    }

    @Test
    fun pdfSpeechReadsVisiblePageAdvancesAndStopsOnManualNavigation() {
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/pdf")
                .setBody(Buffer().write(createPdfFixture())),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]
        val speechEngine = FakeBookSpeechEngine()

        composeRule.setContent {
            CompositionLocalProvider(LocalBookSpeechEngineFactory provides { speechEngine }) {
                MaterialTheme {
                    BookReaderScreen(itemId = "reader-speech-pdf-${System.nanoTime()}", onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed() }.isSuccess
        }
        if (composeRule.onAllNodesWithContentDescription("Ler PDF em voz alta").fetchSemanticsNodes().isEmpty()) {
            assertFalse(
                "PDF narration control must be hidden when platform extraction is unavailable",
                supportsPdfTextExtraction(
                    Build.VERSION.SDK_INT,
                    SdkExtensions.getExtensionVersion(Build.VERSION_CODES.S),
                ),
            )
            composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()
            return
        }
        composeRule.onNodeWithContentDescription("Ler PDF em voz alta").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.waitUntil(timeoutMillis = 5_000) {
            runCatching {
                composeRule.onNodeWithContentDescription("Parar leitura em voz alta").assertIsDisplayed()
            }.isSuccess
        }
        assertTrue(speechEngine.spokenChunks.isEmpty())
        composeRule.onNodeWithContentDescription("Aumentar velocidade da narração").assertIsNotEnabled()
        composeRule.onNodeWithContentDescription("Diminuir velocidade da narração").assertIsNotEnabled()
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.spokenChunks.size == 1 }
        assertEquals(listOf("PDF page 1"), speechEngine.spokenChunks)
        composeRule.onNodeWithContentDescription("Diminuir velocidade da narração").assertIsEnabled()
        composeRule.onNodeWithContentDescription("Aumentar velocidade da narração")
            .assertIsDisplayed()
            .assertIsEnabled()
            .performClick()
        composeRule.onNodeWithText("125%").assertIsDisplayed()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            speechEngine.speechRates.lastOrNull() == 1.25f
        }

        composeRule.runOnUiThread {
            speechEngine.listener.onUtteranceFinished(speechEngine.utteranceIds.last())
        }
        composeRule.waitUntil(timeoutMillis = 5_000) {
            speechEngine.spokenChunks.size == 2 &&
                runCatching { composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed() }.isSuccess
        }
        assertEquals("PDF page 2", speechEngine.spokenChunks.last())
        composeRule.runOnUiThread {
            speechEngine.listener.onUtteranceFinished(speechEngine.utteranceIds.last())
        }
        composeRule.waitUntil(timeoutMillis = 5_000) {
            runCatching { composeRule.onNodeWithContentDescription("Ler PDF em voz alta").assertIsDisplayed() }.isSuccess
        }

        composeRule.onNodeWithContentDescription("Ler PDF em voz alta").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.spokenChunks.size == 3 }
        assertEquals("PDF page 2", speechEngine.spokenChunks.last())
        composeRule.onNodeWithContentDescription("Página anterior").performClick()

        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.stopCount > 0 }
        composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Ler PDF em voz alta").assertIsDisplayed()
    }

    @Test
    fun speechStopsWhenUserScrollsToAnotherTextChunk() {
        val itemId = "reader-speech-scroll-${System.nanoTime()}"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Texto longo. ".repeat(300)),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]
        val speechEngine = FakeBookSpeechEngine()

        composeRule.setContent {
            CompositionLocalProvider(LocalBookSpeechEngineFactory provides { speechEngine }) {
                MaterialTheme {
                    BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 4").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler trecho em voz alta").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.spokenChunks.size == 1 }

        composeRule.onNodeWithTag(PLAIN_TEXT_BOOK_READER_TEST_TAG).performScrollToIndex(3)

        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.stopCount > 0 }
        composeRule.waitForIdle()
        composeRule.onNodeWithContentDescription("Ler trecho em voz alta").assertIsDisplayed()
    }

    @Test
    fun speechStopsBeforeNavigatingBackFromTheReader() {
        val itemId = "reader-speech-back-${System.nanoTime()}"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Conteúdo do livro."),
        )
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        val viewModel = ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
            },
        )[BookReaderViewModel::class.java]
        val speechEngine = FakeBookSpeechEngine()
        val backCount = AtomicInteger()

        composeRule.setContent {
            CompositionLocalProvider(LocalBookSpeechEngineFactory provides { speechEngine }) {
                MaterialTheme {
                    BookReaderScreen(itemId = itemId, onBack = { backCount.incrementAndGet() }, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 1").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler trecho em voz alta").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.spokenChunks.size == 1 }

        composeRule.onNodeWithContentDescription("Voltar").performClick()

        assertEquals(1, backCount.get())
        assertTrue("Leaving the reader must stop speech", speechEngine.stopCount > 0)
    }

    private class FakeBookSpeechEngine : BookSpeechEngine {
        lateinit var listener: BookSpeechEngine.Listener
        var initializeRequested = false
        var stopCount = 0
        var shutdownCount = 0
        val speechRates = mutableListOf<Float>()
        val spokenChunks = mutableListOf<String>()
        val utteranceIds = mutableListOf<String>()

        override fun initialize(listener: BookSpeechEngine.Listener) {
            initializeRequested = true
            this.listener = listener
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

        override fun stop() { stopCount++ }
        override fun shutdown() { shutdownCount++ }
    }

    private class SpeechTestLifecycleOwner : LifecycleOwner {
        val registry = LifecycleRegistry(this)
        override val lifecycle: Lifecycle get() = registry
    }

    private fun createEpub(): ByteArray {
        val output = ByteArrayOutputStream()
        ZipOutputStream(output).use { zip ->
            val mimetype = "application/epub+zip".toByteArray(Charsets.US_ASCII)
            val mimetypeEntry = ZipEntry("mimetype").apply {
                method = ZipEntry.STORED
                size = mimetype.size.toLong()
                compressedSize = mimetype.size.toLong()
                crc = CRC32().apply { update(mimetype) }.value
            }
            zip.putNextEntry(mimetypeEntry)
            zip.write(mimetype)
            zip.closeEntry()
            zip.writeEntry(
                "META-INF/container.xml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                      <rootfiles><rootfile full-path="OPS/package.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/package.opf",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:identifier id="book-id">urn:uuid:mulletaflix-reader-screen-test</dc:identifier>
                        <dc:title>Leitura integrada</dc:title><dc:language>pt-BR</dc:language>
                      </metadata>
                      <manifest>
                        <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                        <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter2" href="chapter2.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="chapter"/><itemref idref="chapter2"/></spine>
                    </package>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/nav.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" lang="pt-BR">
                      <head><title>Sumário</title></head>
                      <body><nav epub:type="toc"><ol>
                        <li><span>Parte I</span><ol>
                          <li><a href="chapter2.xhtml">Capítulo 2</a></li>
                        </ol></li>
                      </ol></nav></body>
                    </html>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/chapter.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
                      <head><title>Capítulo</title></head>
                      <body><h1>Leitura integrada funcionando</h1><p>${(1..24).joinToString(" ") { "Conteúdo EPUB entregue pela fixture HTTP. A posição narrada permanece sincronizada com o capítulo." }}</p></body>
                    </html>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/chapter2.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
                      <head><title>Capítulo 2</title></head>
                      <body><h1>Capítulo 2</h1><p>Conteúdo exclusivo do segundo capítulo.</p></body>
                    </html>""".trimIndent(),
            )
        }
        return output.toByteArray()
    }

    private fun createPdfFixture(): ByteArray {
        val output = ByteArrayOutputStream()
        val document = PdfDocument()
        try {
            repeat(2) { index ->
                val page = document.startPage(PdfDocument.PageInfo.Builder(600, 900, index + 1).create())
                page.canvas.drawColor(Color.WHITE)
                page.canvas.drawText(
                    "PDF page ${index + 1}",
                    48f,
                    96f,
                    Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.BLACK; textSize = 32f },
                )
                document.finishPage(page)
            }
            document.writeTo(output)
            return output.toByteArray()
        } finally {
            document.close()
        }
    }

    private fun awaitWebViewText(expectedText: String): String {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(15)
        while (System.nanoTime() < deadline) {
            val callback = CountDownLatch(1)
            val bodyTexts = AtomicReference<List<String>>(emptyList())
            composeRule.activity.runOnUiThread {
                val webViews = findWebViews(composeRule.activity.window.decorView)
                if (webViews.isEmpty()) {
                    callback.countDown()
                } else {
                    val remaining = AtomicInteger(webViews.size)
                    webViews.forEach { webView ->
                        webView.evaluateJavascript("document.body.innerText") { result ->
                            val text = runCatching {
                                org.json.JSONTokener(result).nextValue() as? String
                            }.getOrNull().orEmpty()
                            bodyTexts.updateAndGet { current -> current + text }
                            if (remaining.decrementAndGet() == 0) callback.countDown()
                        }
                    }
                }
            }
            assertTrue("Le WebView não respondeu à leitura do documento.", callback.await(2, TimeUnit.SECONDS))
            val currentTexts = bodyTexts.get()
            if (currentTexts.any { it.contains(expectedText) }) return currentTexts.joinToString("\n")
            Thread.sleep(100)
        }
        return bodyTextFailure(expectedText)
    }

    private fun findWebViews(view: View): List<WebView> = when (view) {
        is WebView -> listOf(view)
        is ViewGroup -> (0 until view.childCount).flatMap { index -> findWebViews(view.getChildAt(index)) }
        else -> emptyList()
    }

    private fun bodyTextFailure(expectedText: String): Nothing =
        throw AssertionError("O texto esperado não foi encontrado no WebView: $expectedText")

    private fun ZipOutputStream.writeEntry(path: String, content: String) {
        putNextEntry(ZipEntry(path))
        write(content.toByteArray(Charsets.UTF_8))
        closeEntry()
    }

    private class TestSessionRepository(
        private val scope: HomeFeedCacheScope,
    ) : SessionRepository {
        override fun getAccessToken(): Flow<String?> = flowOf(null)
        override fun getDeviceId(): Flow<String> = flowOf("reader-screen-test-device")
        override fun getBaseUrl(): Flow<String> = flowOf(scope.serverUrl)
        override fun getCurrentUserId(): Flow<String?> = flowOf(scope.userId)
        override fun getHomeFeedCacheScope(): Flow<HomeFeedCacheScope?> = flowOf(scope)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
        override fun getSavedServers(): Flow<List<SavedServerSession>> = flowOf(emptyList())
    }
}
