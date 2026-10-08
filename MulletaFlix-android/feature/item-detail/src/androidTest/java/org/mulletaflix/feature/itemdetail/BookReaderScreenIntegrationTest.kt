package org.mulletaflix.feature.itemdetail

import android.view.View
import android.view.ViewGroup
import android.webkit.WebView
import androidx.activity.ComponentActivity
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertHasNoClickAction
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.assert
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.performClick
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.ViewModelStore
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
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okio.Buffer
import org.junit.After
import org.junit.Assert.assertEquals
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
        composeRule.onNodeWithText("100%").assertIsDisplayed()
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

        composeRule.onNodeWithContentDescription("Aumentar tamanho do texto").performClick()
        composeRule.onNodeWithText("110%").assertIsDisplayed()

        val firstRequest = server.takeRequest()
        val retryRequest = server.takeRequest()
        assertEquals("/BookReader/Items/reader-screen-book/BookReader/Epub", firstRequest.path)
        assertEquals(firstRequest.path, retryRequest.path)
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
                      <body><h1>Leitura integrada funcionando</h1><p>Conteúdo EPUB entregue pela fixture HTTP.</p></body>
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
