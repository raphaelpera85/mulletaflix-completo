package org.mulletaflix.feature.itemdetail

import android.graphics.Color
import android.graphics.Paint
import android.graphics.Bitmap
import android.graphics.Canvas
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
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.assertHasNoClickAction
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.assert
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
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
import kotlinx.coroutines.awaitCancellation
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
    fun epubSearchFindsAndNavigatesToMatchingText() {
        val itemId = "reader-epub-search-${System.nanoTime()}"
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

        composeRule.waitUntil(timeoutMillis = 30_000) {
            runCatching {
                composeRule.onNodeWithContentDescription("Buscar no livro").assertIsDisplayed()
            }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Buscar no livro").performClick()
        composeRule.onNodeWithText("Texto").performClick()
        composeRule.onNodeWithText("Texto").performTextInput("exclusivo")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.onAllNodesWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0")
                .fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0").performClick()
        composeRule.waitUntil(timeoutMillis = 15_000) {
            awaitWebViewText("Conteúdo exclusivo do segundo capítulo.")
                .contains("Conteúdo exclusivo do segundo capítulo.")
        }
    }

    @Test
    fun unsupportedGenericPayloadShowsActionableFormatMessageInReader() {
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/octet-stream")
                .setBody(Buffer().write(byteArrayOf(0, 1, 2, 3, 4, 5, 0x7F))),
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
                BookReaderScreen(itemId = "reader-unsupported-generic", onBack = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching {
                composeRule.onNodeWithText(
                    "O arquivo recebido não corresponde a um formato de livro compatível.",
                    useUnmergedTree = true,
                ).assertIsDisplayed()
            }.isSuccess
        }
        assertEquals(1, server.requestCount)
        assertTrue(
            server.takeRequest().path.orEmpty().contains(
                "/BookReader/Items/reader-unsupported-generic/BookReader/Epub",
            ),
        )
    }

    @Test
    fun genericBinaryMimeWithReadableUtf8IsRenderedAsPlainText() {
        val itemId = "reader-generic-text-${System.nanoTime()}"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/octet-stream")
                .setBody(Buffer().write("Capítulo simples\nAção e leitura servidas como binário genérico.".toByteArray())),
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

        composeRule.waitUntil(timeoutMillis = 20_000) {
            viewModel.state.value.textBook != null || viewModel.state.value.error != null
        }
        assertNull(
            "Generic readable text should load instead of failing: ${viewModel.state.value.error}",
            viewModel.state.value.error,
        )
        assertEquals(
            "Capítulo simples\nAção e leitura servidas como binário genérico.",
            viewModel.state.value.textBook?.chunks?.joinToString(""),
        )
        composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
            .assertIsDisplayed()
            .assertTextContains("Capítulo simples", substring = true)
            .assertTextContains("Ação e leitura servidas como binário genérico.", substring = true)
        composeRule.onNodeWithContentDescription("Aparência da leitura")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithText("Sépia").assertIsDisplayed().performClick()
        composeRule.onNodeWithTag(PLAIN_TEXT_BOOK_READER_TEST_TAG)
            .assertIsDisplayed()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            runBlocking { BookReaderProgressStore(context).readAppearance(sessionScope) } ==
                BookReaderAppearance.SEPIA
        }
        assertEquals(BookReaderAppearance.SEPIA, viewModel.state.value.appearance)
        assertEquals(
            "/BookReader/Items/$itemId/BookReader/Epub",
            server.takeRequest(5, TimeUnit.SECONDS)?.path,
        )
    }

    @Test
    fun mobiResponseFromServerIsDecodedAndRenderedInTheReader() {
        val itemId = "reader-mobi-${System.nanoTime()}"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", MobiBookTextExtractor.CONTENT_TYPE)
                .setBody(Buffer().write(createMobi("<h1>Capítulo MOBI</h1><p>Texto MOBI café entregue via HTTP.</p>"))),
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

        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching {
                composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                    .assertIsDisplayed()
                    .assertTextContains("Capítulo MOBI", substring = true)
                    .assertTextContains("Texto MOBI café entregue via HTTP.", substring = true)
            }.isSuccess
        }
        assertEquals(
            "/BookReader/Items/$itemId/BookReader/Epub",
            server.takeRequest(5, TimeUnit.SECONDS)?.path,
        )
    }

    @Test
    fun mobiContentsNavigateToChapterAndRestoreTheSavedLocator() {
        val itemId = "reader-mobi-toc-${System.nanoTime()}"
        val mobi = createMobi(
            "<h1>Capítulo Um</h1><p>${"Texto inicial. ".repeat(110)}</p>" +
                "<h1>Capítulo Dois</h1><p>Conteúdo exclusivo do segundo capítulo MOBI.</p>",
        )
        fun enqueueMobiResponse() {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .addHeader("Content-Type", MobiBookTextExtractor.CONTENT_TYPE)
                    .setBody(Buffer().write(mobi)),
            )
        }
        val api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        fun createReaderViewModel(store: ViewModelStore): BookReaderViewModel =
            ViewModelProvider(
                store,
                object : ViewModelProvider.Factory {
                    @Suppress("UNCHECKED_CAST")
                    override fun <T : ViewModel> create(modelClass: Class<T>): T =
                        BookReaderViewModel(api, TestSessionRepository(sessionScope), context) as T
                },
            )[BookReaderViewModel::class.java]

        enqueueMobiResponse()
        val viewModel = createReaderViewModel(viewModelStore)
        composeRule.setContent {
            MaterialTheme {
                BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
            }
        }
        composeRule.waitUntil(timeoutMillis = 20_000) {
            viewModel.state.value.textBook?.chapters?.any { it.title == "Capítulo Dois" } == true
        }
        val targetChapter = requireNotNull(viewModel.state.value.textBook)
            .chapters.single { it.title == "Capítulo Dois" }
        assertTrue("Fixture must place the second chapter after the first text chunk.", targetChapter.chunkIndex > 0)

        composeRule.onNodeWithContentDescription("Abrir sumário").assertIsEnabled().performClick()
        composeRule.onNodeWithText("Capítulo Dois").assertIsDisplayed().performClick()
        composeRule.waitUntil(timeoutMillis = 10_000) {
            runCatching {
                composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-${targetChapter.chunkIndex}")
                    .assertIsDisplayed()
                    .assertTextContains("Conteúdo exclusivo do segundo capítulo MOBI.", substring = true)
            }.isSuccess && runBlocking {
                BookReaderProgressStore(context).read(sessionScope, itemId)
                    ?.href?.toString() == "mulletaflix-text-chunk-${targetChapter.chunkIndex}"
            }
        }

        val restoredStore = ViewModelStore()
        try {
            enqueueMobiResponse()
            val restoredViewModel = createReaderViewModel(restoredStore)
            composeRule.runOnUiThread { restoredViewModel.load(itemId) }
            composeRule.waitUntil(timeoutMillis = 20_000) {
                restoredViewModel.state.value.initialLocator?.href?.toString() ==
                    "mulletaflix-text-chunk-${targetChapter.chunkIndex}"
            }
            assertEquals(
                "mulletaflix-text-chunk-${targetChapter.chunkIndex}",
                restoredViewModel.state.value.initialLocator?.href?.toString(),
            )
        } finally {
            restoredStore.clear()
        }
        repeat(2) {
            assertEquals(
                "/BookReader/Items/$itemId/BookReader/Epub",
                server.takeRequest(5, TimeUnit.SECONDS)?.path,
            )
        }
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
    fun truncatedRtfShowsErrorWithoutPartialTextAndRetryLoadsValidRtf() {
        val itemId = "reader-screen-truncated-rtf-${System.nanoTime()}"
        val errorMessage = "O RTF contém um conteúdo binário incompleto."
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/rtf")
                .setBody("{\\rtf1\\ansi Texto parcial \\bin4ab"),
        )
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/rtf")
                .setBody("{\\rtf1\\ansi Texto íntegro após retry.}"),
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
        composeRule.onNodeWithText("Texto parcial", useUnmergedTree = true).assertDoesNotExist()
        composeRule.onNodeWithContentDescription("Tentar carregar o livro novamente")
            .assertIsDisplayed()
            .performClick()

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching {
                composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                    .assertTextContains("Texto íntegro após retry.")
            }.isSuccess
        }
        composeRule.onNodeWithText(errorMessage, useUnmergedTree = true).assertDoesNotExist()
        assertEquals(
            "/BookReader/Items/$itemId/BookReader/Epub",
            server.takeRequest().path,
        )
        assertEquals(
            "/BookReader/Items/$itemId/BookReader/Epub",
            server.takeRequest().path,
        )
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
        composeRule.onNodeWithTag("book-speech-language").assertIsDisplayed()
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
    fun plainTextReaderExposesInternalAnchorAsAccessibleLinkAndSelectsTargetChunk() {
        val htmlFile = context.cacheDir.resolve("reader-anchor-${System.nanoTime()}.html")
        htmlFile.writeText(
            "<p><a href='#part-two'>Abrir capítulo dois</a></p>" +
                "<p>${"texto intermediário ".repeat(180)}</p>" +
                "<h2 id='part-two'>Capítulo dois</h2><p>Texto de destino.</p>",
        )
        val document = PlainTextBookDocument.open(htmlFile, "text/html")
        val targetChunk = document.linksByChunk.values.flatten().single().targetChunk
        val selectedChunk = AtomicInteger(-1)
        assertTrue("A âncora deve apontar para um trecho posterior.", targetChunk > 0)

        try {
            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = 0,
                        fontSizePercent = 100,
                        onChunkSelected = selectedChunk::set,
                    )
                }
            }
            composeRule.onNodeWithText("Abrir capítulo dois").assertHasClickAction().performClick()
            composeRule.waitForIdle()

            assertEquals(targetChunk, selectedChunk.get())
        } finally {
            htmlFile.delete()
        }
    }

    @Test
    fun plainTextReaderScrollsToAnchorWhenTargetIsInTheSameChunk() {
        val htmlFile = context.cacheDir.resolve("reader-same-chunk-anchor-${System.nanoTime()}.html")
        htmlFile.writeText(
            "<p><a href='#inside'>Ir para o destino</a></p>" +
                "<p>${"Texto intermediário. ".repeat(22)}</p>" +
                "<p id='inside'>Destino exato do link.</p>",
        )
        val document = PlainTextBookDocument.open(htmlFile, "text/html")
        val link = document.linksByChunk.values.flatten().single()
        val selectedChunk = AtomicInteger(-1)
        assertEquals(0, link.targetChunk)
        assertTrue("O destino deve estar abaixo do início do mesmo trecho.", link.targetOffset > 0)

        try {
            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = 0,
                        fontSizePercent = 100,
                        onChunkSelected = selectedChunk::set,
                    )
                }
            }
            composeRule.onNodeWithText("Ir para o destino").assertHasClickAction()
            val chunkNode = composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
            val beforeTop = chunkNode.fetchSemanticsNode().boundsInRoot.top
            composeRule.onNodeWithText("Ir para o destino").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                chunkNode.fetchSemanticsNode().boundsInRoot.top < beforeTop - 20f
            }

            assertEquals(0, selectedChunk.get())
        } finally {
            htmlFile.delete()
        }
    }

    @Test
    fun textBookNarrationLanguageCanBeSelectedAndResetToDeviceDefault() {
        val suffix = System.nanoTime()
        val itemId = "reader-language-$suffix"
        val scopedSession = sessionScope.copy(serverId = "reader-language-server-$suffix")
        val progressStore = BookReaderProgressStore(context)
        runBlocking { progressStore.writeSpeechLanguageTag(scopedSession, null) }
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Conteúdo para narração. ".repeat(90)),
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

        composeRule.setContent {
            MaterialTheme {
                CompositionLocalProvider(LocalBookSpeechEngineFactory provides { speechEngine }) {
                    BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithTag("book-speech-language").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.onNodeWithText("Idioma da narração").assertIsDisplayed()
        composeRule.onNodeWithTag("speech-language-en-US").performClick()
        composeRule.waitUntil(timeoutMillis = 10_000) {
            viewModel.state.value.speechLanguageTag == "en-US" &&
                speechEngine.selectedLanguageTag == "en-US" &&
                runBlocking { progressStore.readSpeechLanguageTag(scopedSession) } == "en-US"
        }

        composeRule.onNodeWithTag("book-speech-language").performClick()
        composeRule.onNodeWithTag("speech-language-default").performClick()
        composeRule.waitUntil(timeoutMillis = 10_000) {
            viewModel.state.value.speechLanguageTag == null &&
                speechEngine.selectedLanguageTag == null &&
                runBlocking { progressStore.readSpeechLanguageTag(scopedSession) } == null
        }
    }

    @Test
    fun textBookNarrationVoiceCanBeSelectedAndPersisted() {
        val suffix = System.nanoTime()
        val itemId = "reader-voice-$suffix"
        val scopedSession = sessionScope.copy(serverId = "reader-voice-server-$suffix")
        val progressStore = BookReaderProgressStore(context)
        runBlocking { progressStore.writeSpeechVoiceName(scopedSession, null) }
        runBlocking { progressStore.writeSpeechLanguageTag(scopedSession, "pt-BR") }
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Conteúdo para narração. ".repeat(90)),
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

        composeRule.setContent {
            MaterialTheme {
                CompositionLocalProvider(LocalBookSpeechEngineFactory provides { speechEngine }) {
                    BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
                }
            }
        }
        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithTag("book-speech-language").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.onNodeWithTag("speech-language-open-voices").performClick()
        composeRule.onNodeWithText("Voz da narração").assertIsDisplayed()
        composeRule.onAllNodesWithText("Disponível offline").assertCountEquals(2)
        composeRule.onNodeWithText("Requer conexão").assertIsDisplayed()
        composeRule.onNodeWithTag("speech-voice-pt-br-female-local").performClick()
        composeRule.waitUntil(timeoutMillis = 10_000) {
            viewModel.state.value.speechVoiceName == "pt-br-female-local" &&
                speechEngine.selectedVoiceName == "pt-br-female-local" &&
                runBlocking { progressStore.readSpeechVoiceName(scopedSession) } == "pt-br-female-local"
        }
        composeRule.onNodeWithTag("book-speech-language").performClick()
        composeRule.onNodeWithTag("speech-language-open-voices").performClick()
        composeRule.onNodeWithTag("speech-voice-pt-br-female-local").assertIsSelected()
    }

    @Test
    fun staleSavedNarrationLanguageCanBeRecoveredFromTheLanguageSelector() {
        val suffix = System.nanoTime()
        val itemId = "reader-stale-language-$suffix"
        val scopedSession = sessionScope.copy(serverId = "reader-stale-language-server-$suffix")
        val progressStore = BookReaderProgressStore(context)
        runBlocking { progressStore.writeSpeechLanguageTag(scopedSession, "it-IT") }
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody("Conteúdo para narração. ".repeat(90)),
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

        composeRule.setContent {
            MaterialTheme {
                CompositionLocalProvider(LocalBookSpeechEngineFactory provides { speechEngine }) {
                    BookReaderScreen(itemId = itemId, onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Trecho 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.waitUntil(timeoutMillis = 5_000) {
            viewModel.state.value.speechLanguageTag == "it-IT"
        }
        composeRule.onNodeWithTag("book-speech-language").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }

        composeRule.onNodeWithText("Idioma da narração").assertIsDisplayed()
        composeRule.onNodeWithTag("speech-language-default").assertIsDisplayed()
        composeRule.waitUntil(timeoutMillis = 10_000) {
            viewModel.state.value.speechLanguageTag == null &&
                speechEngine.selectedLanguageTag == null &&
                runBlocking { progressStore.readSpeechLanguageTag(scopedSession) } == null
        }
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
    fun pdfSearchFindsAndNavigatesToMatchingPageWhenPlatformSupportsExtraction() {
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

        composeRule.setContent {
            MaterialTheme {
                BookReaderScreen(itemId = "reader-pdf-search-${System.nanoTime()}", onBack = {}, viewModel = viewModel)
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed() }.isSuccess
        }
        val searchControl = composeRule.onAllNodesWithContentDescription("Buscar no livro").fetchSemanticsNodes()
        if (searchControl.isEmpty()) {
            assertFalse(
                "PDF search must be hidden when platform text extraction is unavailable",
                supportsPdfTextExtraction(
                    Build.VERSION.SDK_INT,
                    SdkExtensions.getExtensionVersion(Build.VERSION_CODES.S),
                ),
            )
            return
        }

        composeRule.onNodeWithContentDescription("Buscar no livro").performClick()
        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("page 2")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(timeoutMillis = 10_000) {
            composeRule.onAllNodesWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0")
            .assertIsDisplayed()
            .performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            runCatching { composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed() }.isSuccess
        }
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
    fun switchingBookCancelsPendingPageNarrationExtraction() {
        val firstItemId = "reader-speech-cancel-${System.nanoTime()}"
        val nextItemId = "$firstItemId-next"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/pdf")
                .setBody(Buffer().write(createPdfFixture(pageCount = 2))),
        )
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/pdf")
                .setBody(Buffer().write(createPdfFixture(pageCount = 3))),
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
        val readerItemId = mutableStateOf(firstItemId)
        val extractionStarted = CountDownLatch(1)
        val extractionCancelled = CountDownLatch(1)
        val speechEngine = FakeBookSpeechEngine()

        composeRule.setContent {
            CompositionLocalProvider(
                LocalBookSpeechEngineFactory provides { speechEngine },
                LocalBookPageTextExtractor provides { _, _, _ ->
                    extractionStarted.countDown()
                    try {
                        awaitCancellation()
                    } finally {
                        extractionCancelled.countDown()
                    }
                },
            ) {
                MaterialTheme {
                    BookReaderScreen(itemId = readerItemId.value, onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler PDF em voz alta").performClick()
        assertTrue("Page extraction should start", extractionStarted.await(15, TimeUnit.SECONDS))

        composeRule.runOnUiThread { readerItemId.value = nextItemId }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 3").assertIsDisplayed() }.isSuccess
        }
        assertTrue(
            "Changing books must cancel pending page extraction",
            extractionCancelled.await(15, TimeUnit.SECONDS),
        )
        assertEquals("The stale extraction must not start speech", emptyList<String>(), speechEngine.spokenChunks)
        assertEquals(
            "/BookReader/Items/$firstItemId/BookReader/Epub",
            server.takeRequest().path,
        )
        assertEquals(
            "/BookReader/Items/$nextItemId/BookReader/Epub",
            server.takeRequest().path,
        )
    }

    @Test
    fun switchingBookStopsActivePageNarrationAndIgnoresLateSpeechCallback() {
        val firstItemId = "reader-speech-active-${System.nanoTime()}"
        val nextItemId = "$firstItemId-next"
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/pdf")
                .setBody(Buffer().write(createPdfFixture(pageCount = 2))),
        )
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "application/pdf")
                .setBody(Buffer().write(createPdfFixture(pageCount = 3))),
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
        val engines = java.util.Collections.synchronizedList(mutableListOf<FakeBookSpeechEngine>())
        val speechEngineFactory: (android.content.Context) -> BookSpeechEngine = {
            FakeBookSpeechEngine().also(engines::add)
        }
        val readerItemId = mutableStateOf(firstItemId)
        val lifecycleOwner = SpeechTestLifecycleOwner()
        composeRule.runOnUiThread {
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
        }

        composeRule.setContent {
            CompositionLocalProvider(
                LocalLifecycleOwner provides lifecycleOwner,
                LocalBookSpeechEngineFactory provides speechEngineFactory,
                LocalBookPageTextExtractor provides { _, _, pageIndex ->
                    "Narration A page $pageIndex. ".repeat(100)
                },
            ) {
                MaterialTheme {
                    BookReaderScreen(itemId = readerItemId.value, onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler PDF em voz alta").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { engines.firstOrNull()?.initializeRequested == true }
        val firstEngine = engines.first()
        val firstListener = firstEngine.listener
        composeRule.runOnUiThread { firstListener.onReady() }
        composeRule.waitUntil(timeoutMillis = 5_000) { firstEngine.spokenChunks.size == 1 }
        val activeUtteranceId = firstEngine.utteranceIds.single()

        composeRule.runOnUiThread { readerItemId.value = nextItemId }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            engines.size >= 2 &&
                runCatching { composeRule.onNodeWithText("Página 1 de 3").assertIsDisplayed() }.isSuccess
        }
        composeRule.waitUntil(timeoutMillis = 5_000) { firstEngine.stopCount > 0 }
        composeRule.runOnUiThread { firstListener.onUtteranceFinished(activeUtteranceId) }

        assertTrue("The old engine must stop after an item switch", firstEngine.stopCount > 0)
        assertEquals("Late completion must not start narration for another page", 1, firstEngine.spokenChunks.size)
        assertTrue("The new book must not inherit old narration", engines[1].spokenChunks.isEmpty())
        assertEquals("/BookReader/Items/$firstItemId/BookReader/Epub", server.takeRequest().path)
        assertEquals("/BookReader/Items/$nextItemId/BookReader/Epub", server.takeRequest().path)

        composeRule.runOnUiThread {
            lifecycleOwner.registry.handleLifecycleEvent(Lifecycle.Event.ON_DESTROY)
        }
    }

    @Test(timeout = 60_000)
    fun comicSpeechRecognizesCurrentPageOfflineAndAdvancesAfterUtterance() {
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", ComicBookArchive.CONTENT_TYPE)
                .setBody(Buffer().write(createTextComicCbz("MULLETA PAGE ONE", "MULLETA PAGE TWO"))),
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
                    BookReaderScreen(itemId = "reader-speech-comic-${System.nanoTime()}", onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler quadrinhos em voz alta").assertIsDisplayed().performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.waitUntil(timeoutMillis = 15_000) { speechEngine.spokenChunks.isNotEmpty() }
        assertTrue("Expected OCR text from page 1, got ${speechEngine.spokenChunks}", speechEngine.spokenChunks.first().contains("PAGE ONE"))

        composeRule.runOnUiThread {
            speechEngine.listener.onUtteranceFinished(speechEngine.utteranceIds.last())
        }
        composeRule.waitUntil(timeoutMillis = 15_000) {
            speechEngine.spokenChunks.size >= 2 &&
                runCatching { composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed() }.isSuccess
        }
        assertTrue("Expected OCR text from page 2, got ${speechEngine.spokenChunks}", speechEngine.spokenChunks.last().contains("PAGE TWO"))
    }

    @Test(timeout = 60_000)
    fun cbrSearchResultOpensMatchingPage() {
        val cbrBytes = context.assets.open("books/cbr-ocr-pages.cbr").use { it.readBytes() }
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", CbrBookArchive.CONTENT_TYPE)
                .setBody(Buffer().write(cbrBytes)),
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
                BookReaderScreen(
                    itemId = "reader-search-cbr-${System.nanoTime()}",
                    onBack = {},
                    viewModel = viewModel,
                )
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Buscar no livro").performClick()
        composeRule.onNodeWithText("Texto").performClick().performTextInput("page two")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) {
            composeRule.onAllNodesWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0")
                .fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0")
            .assertTextContains("Página 2:", substring = true)
            .assertTextContains("PAGE TWO", substring = true)
            .performClick()
        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed() }.isSuccess
        }
    }

    @Test(timeout = 60_000)
    fun cbrSpeechRecognizesCurrentPageOfflineAndAdvancesAfterUtterance() {
        val cbrBytes = context.assets.open("books/cbr-ocr-pages.cbr").use { it.readBytes() }
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", CbrBookArchive.CONTENT_TYPE)
                .setBody(Buffer().write(cbrBytes)),
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
                    BookReaderScreen(itemId = "reader-speech-cbr-${System.nanoTime()}", onBack = {}, viewModel = viewModel)
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            runCatching { composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithContentDescription("Ler quadrinhos em voz alta").assertIsDisplayed().performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) { speechEngine.initializeRequested }
        composeRule.runOnUiThread { speechEngine.listener.onReady() }
        composeRule.waitUntil(timeoutMillis = 15_000) { speechEngine.spokenChunks.isNotEmpty() }
        assertTrue(
            "Expected recognized CBR page 1 text, got ${speechEngine.spokenChunks}",
            speechEngine.spokenChunks.first().contains("MULLETA PAGE ONE"),
        )
        composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()

        composeRule.runOnUiThread {
            speechEngine.listener.onUtteranceFinished(speechEngine.utteranceIds.last())
        }
        composeRule.waitUntil(timeoutMillis = 15_000) {
            speechEngine.spokenChunks.size >= 2 &&
                runCatching { composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed() }.isSuccess
        }
        assertTrue(
            "Expected recognized CBR page 2 text, got ${speechEngine.spokenChunks}",
            speechEngine.spokenChunks.last().contains("MULLETA PAGE TWO"),
        )
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
        var selectedLanguageTag: String? = null
        var selectedVoiceName: String? = null
        var stopCount = 0
        var shutdownCount = 0
        val speechRates = mutableListOf<Float>()
        val spokenChunks = mutableListOf<String>()
        val utteranceIds = mutableListOf<String>()

        override fun initialize(listener: BookSpeechEngine.Listener) {
            initializeRequested = true
            this.listener = listener
        }

        override fun availableLanguages() = listOf(
            BookSpeechLanguageOption("en-US", "English (United States)"),
            BookSpeechLanguageOption("pt-BR", "Português (Brasil)"),
        )

        override fun availableVoices(languageTag: String?) = listOf(
            BookSpeechVoiceOption("en-us-default", "en-US", "English", 500, false),
            BookSpeechVoiceOption("pt-br-female-local", "pt-BR", "Português feminino", 400, false),
            BookSpeechVoiceOption("pt-br-male-online", "pt-BR", "Português masculino", 500, true),
        ).filter { languageTag == null || it.languageTag == languageTag }

        override fun setSpeechLanguage(languageTag: String?): Boolean {
            if (languageTag != null && availableLanguages().none { it.languageTag == languageTag }) return false
            selectedLanguageTag = languageTag
            return true
        }

        override fun setSpeechVoice(voiceName: String?): Boolean {
            if (voiceName != null && availableVoices(null).none { it.name == voiceName }) return false
            selectedVoiceName = voiceName
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

    private fun createMobi(html: String): ByteArray {
        val text = html.toByteArray(Charsets.UTF_8)
        val splitAfterFirstByteOfMultibyteCharacter = text.indexOf(0xC3.toByte()) + 1
        require(splitAfterFirstByteOfMultibyteCharacter > 0)
        val indexingTrailer = ByteArray(128) { 0x42 } + byteArrayOf(0x81.toByte(), 0x02)
        val firstTextRecord = text.copyOfRange(0, splitAfterFirstByteOfMultibyteCharacter) +
            byteArrayOf(text[splitAfterFirstByteOfMultibyteCharacter], 0x01) + indexingTrailer
        val secondTextRecord = text.copyOfRange(splitAfterFirstByteOfMultibyteCharacter, text.size) +
            byteArrayOf(0x00) + indexingTrailer
        val recordCount = 3
        val firstRecordOffset = 78 + recordCount * 8
        val firstRecordLength = 248
        val firstTextRecordOffset = firstRecordOffset + firstRecordLength
        val secondTextRecordOffset = firstTextRecordOffset + firstTextRecord.size
        val result = ByteArray(secondTextRecordOffset + secondTextRecord.size)
        writeAscii(result, 0, "Mulletaflix MOBI test")
        writeAscii(result, 60, "BOOK")
        writeAscii(result, 64, "MOBI")
        writeUInt16(result, 76, recordCount)
        writeUInt32(result, 78, firstRecordOffset)
        writeUInt32(result, 86, firstTextRecordOffset)
        writeUInt32(result, 94, secondTextRecordOffset)
        writeUInt16(result, firstRecordOffset, 1)
        writeUInt32(result, firstRecordOffset + 4, text.size)
        writeUInt16(result, firstRecordOffset + 8, 2)
        writeUInt16(result, firstRecordOffset + 10, 4_096)
        writeAscii(result, firstRecordOffset + 16, "MOBI")
        writeUInt32(result, firstRecordOffset + 20, 232)
        writeUInt32(result, firstRecordOffset + 24, 2)
        writeUInt32(result, firstRecordOffset + 28, 65_001)
        writeUInt16(result, firstRecordOffset + 242, 3)
        firstTextRecord.copyInto(result, firstTextRecordOffset)
        secondTextRecord.copyInto(result, secondTextRecordOffset)
        return result
    }

    private fun writeAscii(target: ByteArray, offset: Int, value: String) {
        value.toByteArray(Charsets.US_ASCII).copyInto(target, offset)
    }

    private fun writeUInt16(target: ByteArray, offset: Int, value: Int) {
        target[offset] = (value ushr 8).toByte()
        target[offset + 1] = value.toByte()
    }

    private fun writeUInt32(target: ByteArray, offset: Int, value: Int) {
        target[offset] = (value ushr 24).toByte()
        target[offset + 1] = (value ushr 16).toByte()
        target[offset + 2] = (value ushr 8).toByte()
        target[offset + 3] = value.toByte()
    }

    private fun createPdfFixture(pageCount: Int = 2): ByteArray {
        val output = ByteArrayOutputStream()
        val document = PdfDocument()
        try {
            repeat(pageCount) { index ->
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

    private fun createTextComicCbz(vararg pageLabels: String): ByteArray {
        val output = ByteArrayOutputStream()
        ZipOutputStream(output).use { zip ->
            pageLabels.forEachIndexed { index, label ->
                val bitmap = Bitmap.createBitmap(1400, 700, Bitmap.Config.ARGB_8888)
                try {
                    Canvas(bitmap).apply {
                        drawColor(Color.WHITE)
                        drawText(
                            label,
                            55f,
                            390f,
                            Paint(Paint.ANTI_ALIAS_FLAG).apply {
                                color = Color.BLACK
                                textSize = 118f
                                typeface = android.graphics.Typeface.create("sans-serif", android.graphics.Typeface.BOLD)
                            },
                        )
                    }
                    zip.putNextEntry(ZipEntry("page-${index + 1}.png"))
                    check(bitmap.compress(Bitmap.CompressFormat.PNG, 100, zip))
                    zip.closeEntry()
                } finally {
                    bitmap.recycle()
                }
            }
        }
        return output.toByteArray()
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
