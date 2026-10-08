package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import io.mockk.coEvery
import io.mockk.mockk
import java.io.ByteArrayOutputStream
import java.nio.charset.Charset
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody
import okhttp3.ResponseBody.Companion.toResponseBody
import okio.BufferedSource
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SavedServerSession
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.BookReaderStatusDto
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import retrofit2.HttpException
import retrofit2.Response

/**
 * Covers the pendency left open by the HTTP contract task (TODO-APP.md, "Contrato HTTP do
 * leitor de livros EPUB/CBZ"): [BookReaderViewModel] exercised against a real [Context] and
 * DataStore through Robolectric, since it depends on `@ApplicationContext` and cannot run as a
 * plain JVM test without it. Covers loading/error/retry and session isolation, as suggested by
 * the handover (`HANDOVER-ANDROID-LEITOR-LIVROS.md`, pendência 6).
 */
@OptIn(ExperimentalCoroutinesApi::class)
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class BookReaderViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private val context: Context = ApplicationProvider.getApplicationContext()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun `load starts in loading state and resolves to the parsed publication`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } returns epubResponseBody()
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("item-1")
        assertTrue(viewModel.state.value.isLoading)

        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertEquals("Integration Test Book", state.publication?.metadata?.title)
    }

    @Test
    fun `load reads directly returned HTML as safe text`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } returns
            """<html><head><script>window.bad=true</script></head><body><h1>HTML direto</h1><p>Ação café.</p></body></html>"""
                .toByteArray(Charset.forName("windows-1252"))
                .toResponseBody("text/html; charset=windows-1252".toMediaType())
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("direct-html-book")
        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        val text = state.textBook?.chunks?.joinToString("").orEmpty()
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertNull(state.publication)
        assertTrue(text.contains("HTML direto"))
        assertTrue(text.contains("Ação café."))
        assertFalse(text.contains("window.bad"))
    }

    @Test
    fun `load extracts DOCX response into the paginated text reader`() = runTest(dispatcher) {
        val docx = ByteArrayOutputStream().apply {
            ZipOutputStream(this).use { zip ->
                zip.putNextEntry(ZipEntry("[Content_Types].xml"))
                zip.write("<Types/>".toByteArray())
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("word/document.xml"))
                zip.write("""<w:document xmlns:w="urn:word"><w:body><w:p><w:r><w:t>DOCX direto &amp; seguro.</w:t></w:r></w:p></w:body></w:document>""".toByteArray())
                zip.closeEntry()
            }
        }.toByteArray()
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } returns
            docx.toResponseBody(DocxBookTextExtractor.CONTENT_TYPE.toMediaType())
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("direct-docx-book")
        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        val text = state.textBook?.chunks?.joinToString("").orEmpty()
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertNull(state.publication)
        assertTrue(text.contains("DOCX direto & seguro."))
    }

    @Test
    fun `load detects generic ODT response and reads its text directly`() = runTest(dispatcher) {
        val odt = ByteArrayOutputStream().apply {
            ZipOutputStream(this).use { zip ->
                val mimeBytes = OdtBookTextExtractor.CONTENT_TYPE.toByteArray(Charsets.US_ASCII)
                zip.putNextEntry(
                    ZipEntry("mimetype").apply {
                        method = ZipEntry.STORED
                        size = mimeBytes.size.toLong()
                        compressedSize = mimeBytes.size.toLong()
                        crc = CRC32().apply { update(mimeBytes) }.value
                    },
                )
                zip.write(mimeBytes)
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("content.xml"))
                zip.write(
                    """<office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"><office:body><office:text><text:p>ODT direto &amp; seguro.</text:p></office:text></office:body></office:document-content>"""
                        .toByteArray(Charsets.UTF_8),
                )
                zip.closeEntry()
            }
        }.toByteArray()
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } returns
            odt.toResponseBody("application/octet-stream".toMediaType())
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("direct-odt-book")
        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        val text = state.textBook?.chunks?.joinToString("").orEmpty()
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertNull(state.publication)
        assertTrue(text.contains("ODT direto & seguro."))
    }

    @Test
    fun `load detects FictionBook XML when server uses a generic MIME type`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } returns
            """<?xml version="1.0"?><FictionBook><description><title-info><book-title>Hidden metadata</book-title></title-info></description><body><p>FB2 direto — conteúdo &amp; seguro.</p></body></FictionBook>"""
                .toResponseBody("application/octet-stream".toMediaType())
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("direct-fb2-book")
        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        val text = state.textBook?.chunks?.joinToString("").orEmpty()
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertNull(state.publication)
        assertTrue(text.contains("FB2 direto — conteúdo & seguro."))
        assertFalse(text.contains("Hidden metadata"))
    }

    @Test
    fun `load reads compressed FictionBook with a cover and generic MIME before comic detection`() = runTest(dispatcher) {
        val fb2Zip = ByteArrayOutputStream().apply {
            ZipOutputStream(this).use { zip ->
                zip.putNextEntry(ZipEntry("covers/cover.jpg"))
                zip.write(byteArrayOf(1, 2, 3))
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("books/title.fb2"))
                zip.write(
                    """<?xml version="1.0"?><FictionBook><description><title-info><book-title>Hidden metadata</book-title></title-info></description><body><p>FB2 ZIP direto — conteúdo seguro.</p></body></FictionBook>"""
                        .toByteArray(Charsets.UTF_8),
                )
                zip.closeEntry()
            }
        }.toByteArray()
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } returns
            fb2Zip.toResponseBody("application/octet-stream".toMediaType())
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("direct-fb2-zip-book")
        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        val text = state.textBook?.chunks?.joinToString("").orEmpty()
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertNull(state.publication)
        assertTrue(text.contains("FB2 ZIP direto — conteúdo seguro."))
        assertFalse(text.contains("Hidden metadata"))
    }


    @Test
    fun `http error from the server surfaces a retry-able message and clears loading`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } throws httpException(415)
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("item-1")
        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        assertFalse(state.isLoading)
        assertEquals(bookReaderHttpFailureMessage(415), state.error)
        assertNull(state.publication)
    }

    @Test
    fun `retrying after a transient failure loads the book once the server responds`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        coEvery { api.getBookReaderEpub(any()) } throws httpException(503)
        coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("item-1")
        awaitLoadFinished(viewModel)
        assertNotNull(viewModel.state.value.error)
        assertFalse(viewModel.state.value.isLoading)

        coEvery { api.getBookReaderEpub(any()) } answers { epubResponseBody() }
        viewModel.load("item-1")
        dispatcher.scheduler.runCurrent()
        assertTrue(viewModel.state.value.isLoading)
        awaitLoadFinished(viewModel)

        val state = viewModel.state.value
        assertFalse(state.isLoading)
        assertNull(state.error)
        assertEquals("Integration Test Book", state.publication?.metadata?.title)
    }

    @Test
    fun `switching the session account reloads and never restores progress from the previous account`() =
        runTest(dispatcher) {
            val api = mockk<MulletaFlixApiService>(relaxed = true)
            // Each call must return a fresh body: this scenario reloads the same item three
            // times, and a consumed/closed OkHttp ResponseBody cannot be read again.
            coEvery { api.getBookReaderEpub(any()) } answers { epubResponseBody() }
            coEvery { api.getBookReaderStatus(any()) } returns BookReaderStatusDto("Ready")
            val scopeFlow = MutableStateFlow<HomeFeedCacheScope?>(SCOPE_A)
            val sessionRepository = FakeSessionRepository { scopeFlow }
            val viewModel = BookReaderViewModel(api, sessionRepository, context)
            val itemId = "shared-item"

            viewModel.load(itemId)
            awaitLoadFinished(viewModel)
            assertFalse(viewModel.state.value.isLoading)

            // The reading preference (font size) is persisted per account/server scope.
            viewModel.setFontSizePercent(itemId, 150)
            assertEquals(150, viewModel.state.value.fontSizePercent)
            val progressStore = BookReaderProgressStore(context)
            settle(timeoutMs = 5_000) {
                runBlocking { progressStore.readFontSizePercent(SCOPE_A) } == 150
            }

            // Reloading under the same account restores the preference just saved.
            viewModel.load(itemId)
            awaitLoadFinished(viewModel)
            assertEquals(150, viewModel.state.value.fontSizePercent)
            assertNull(viewModel.state.value.error)

            // The session switches to a different account/server while this book stays open.
            scopeFlow.value = SCOPE_B
            awaitLoadFinished(viewModel)

            val stateAfterSwitch = viewModel.state.value
            assertFalse(
                "o estado não deve ficar em loading indefinidamente após a troca de sessão",
                stateAfterSwitch.isLoading,
            )
            assertNull(stateAfterSwitch.error)
            assertEquals(
                "a preferência salva pela conta anterior não pode ser aplicada na nova conta",
                BookReaderFontSize.DEFAULT_PERCENT,
                stateAfterSwitch.fontSizePercent,
            )
            assertEquals("Integration Test Book", stateAfterSwitch.publication?.metadata?.title)
        }

    @Test
    fun `server conversion status is shown while the book stream waits and cleared after it arrives`() =
        runTest(dispatcher) {
            val api = mockk<MulletaFlixApiService>(relaxed = true)
            val pendingBody = CompletableDeferred<ResponseBody>()
            val statusCalls = AtomicInteger()
            coEvery { api.getBookReaderEpub("converting-book") } coAnswers { pendingBody.await() }
            coEvery { api.getBookReaderStatus("converting-book") } coAnswers {
                statusCalls.incrementAndGet()
                BookReaderStatusDto("Converting", "converting-book", "Livro de teste")
            }
            val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

            viewModel.load("converting-book")
            settle(timeoutMs = 7_000) {
                statusCalls.get() > 0 && viewModel.state.value.loadingMessage != null
            }

            assertEquals(
                "O servidor está convertendo este livro para leitura. Aguarde um momento.",
                viewModel.state.value.loadingMessage,
            )
            pendingBody.complete(epubResponseBody())
            awaitLoadFinished(viewModel)

            assertFalse(viewModel.state.value.isLoading)
            assertNull(viewModel.state.value.loadingMessage)
            assertEquals("Integration Test Book", viewModel.state.value.publication?.metadata?.title)
            assertEquals(1, statusCalls.get())
        }

    @Test
    fun `conversion message clears and status polling stops when the book stream fails`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        val pendingBody = CompletableDeferred<ResponseBody>()
        val statusCalls = AtomicInteger()
        coEvery { api.getBookReaderEpub("failed-conversion-book") } coAnswers { pendingBody.await() }
        coEvery { api.getBookReaderStatus("failed-conversion-book") } coAnswers {
            statusCalls.incrementAndGet()
            BookReaderStatusDto("Converting", "failed-conversion-book", "Livro de teste")
        }
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("failed-conversion-book")
        settle(timeoutMs = 7_000) {
            statusCalls.get() > 0 && viewModel.state.value.loadingMessage != null
        }
        pendingBody.completeExceptionally(httpException(415))
        awaitLoadFinished(viewModel)

        assertEquals(bookReaderHttpFailureMessage(415), viewModel.state.value.error)
        assertNull(viewModel.state.value.loadingMessage)
        assertEquals(1, statusCalls.get())
    }

    @Test
    fun `late conversion response cannot restore message after epub response arrives`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        val contentTypeRequested = CountDownLatch(1)
        val allowContentType = CountDownLatch(1)
        val statusRequestStarted = CountDownLatch(1)
        val allowStatusResponse = CountDownLatch(1)
        val statusResponseReturned = CountDownLatch(1)
        val pendingBody = CompletableDeferred<ResponseBody>()
        coEvery { api.getBookReaderEpub("late-status-book") } coAnswers { pendingBody.await() }
        coEvery { api.getBookReaderStatus("late-status-book") } coAnswers {
            statusRequestStarted.countDown()
            withContext(NonCancellable) {
                check(allowStatusResponse.await(5, TimeUnit.SECONDS))
            }
            statusResponseReturned.countDown()
            BookReaderStatusDto("Converting", "late-status-book", "Livro de teste")
        }
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        try {
            viewModel.load("late-status-book")
            settle(timeoutMs = 7_000) { statusRequestStarted.count == 0L }
            pendingBody.complete(GatedEpubResponseBody(epubResponseBody(), contentTypeRequested, allowContentType))
            settle(timeoutMs = 7_000) { contentTypeRequested.count == 0L }
            assertTrue(viewModel.state.value.isLoading)
            assertNull(viewModel.state.value.loadingMessage)

            allowStatusResponse.countDown()
            assertTrue(statusResponseReturned.await(5, TimeUnit.SECONDS))
            Thread.sleep(50)
            assertNull(viewModel.state.value.loadingMessage)
        } finally {
            allowStatusResponse.countDown()
            allowContentType.countDown()
        }

        awaitLoadFinished(viewModel)
        assertNull(viewModel.state.value.error)
        assertNull(viewModel.state.value.loadingMessage)
        assertEquals("Integration Test Book", viewModel.state.value.publication?.metadata?.title)
    }

    @Test
    fun `late conversion response from previous book cannot update the new book state`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        val previousBody = CompletableDeferred<ResponseBody>()
        val statusRequestStarted = CountDownLatch(1)
        val allowStatusResponse = CountDownLatch(1)
        val statusResponseReturned = CountDownLatch(1)
        val newBookContentTypeRequested = CountDownLatch(1)
        val allowNewBookContentType = CountDownLatch(1)
        coEvery { api.getBookReaderEpub("previous-book") } coAnswers { previousBody.await() }
        coEvery { api.getBookReaderEpub("new-book") } returns
            GatedEpubResponseBody(epubResponseBody(), newBookContentTypeRequested, allowNewBookContentType)
        coEvery { api.getBookReaderStatus("previous-book") } coAnswers {
            statusRequestStarted.countDown()
            withContext(NonCancellable) {
                check(allowStatusResponse.await(5, TimeUnit.SECONDS))
            }
            statusResponseReturned.countDown()
            BookReaderStatusDto("Converting", "previous-book", "Livro anterior")
        }
        coEvery { api.getBookReaderStatus("new-book") } returns BookReaderStatusDto("Ready")
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        try {
            viewModel.load("previous-book")
            settle(timeoutMs = 7_000) { statusRequestStarted.count == 0L }
            viewModel.load("new-book")

            allowStatusResponse.countDown()
            assertTrue(statusResponseReturned.await(5, TimeUnit.SECONDS))
            settle(timeoutMs = 7_000) { newBookContentTypeRequested.count == 0L }
            Thread.sleep(50)
            assertTrue(viewModel.state.value.isLoading)
            assertNull(viewModel.state.value.loadingMessage)
        } finally {
            allowStatusResponse.countDown()
            allowNewBookContentType.countDown()
        }

        awaitLoadFinished(viewModel)
        assertNull(viewModel.state.value.error)
        assertNull(viewModel.state.value.loadingMessage)
        assertEquals("Integration Test Book", viewModel.state.value.publication?.metadata?.title)
    }

    @Test
    fun `unavailable conversion status endpoint does not block reading`() = runTest(dispatcher) {
        val api = mockk<MulletaFlixApiService>(relaxed = true)
        val pendingBody = CompletableDeferred<ResponseBody>()
        val statusCalls = AtomicInteger()
        coEvery { api.getBookReaderEpub("legacy-server-book") } coAnswers { pendingBody.await() }
        coEvery { api.getBookReaderStatus("legacy-server-book") } coAnswers {
            statusCalls.incrementAndGet()
            throw httpException(404)
        }
        val viewModel = BookReaderViewModel(api, FakeSessionRepository(SCOPE_A), context)

        viewModel.load("legacy-server-book")
        settle(timeoutMs = 7_000) { statusCalls.get() > 0 }
        assertTrue(viewModel.state.value.isLoading)
        assertNull(viewModel.state.value.loadingMessage)

        pendingBody.complete(epubResponseBody())
        awaitLoadFinished(viewModel)

        assertNull(viewModel.state.value.error)
        assertEquals("Integration Test Book", viewModel.state.value.publication?.metadata?.title)
        assertEquals(1, statusCalls.get())
    }

    /**
     * [BookReaderViewModel.load] copies the response body on the real `Dispatchers.IO`, and
     * `BookReaderProgressStore` persists through DataStore's own dispatcher; neither is driven
     * by [dispatcher]'s virtual clock, so advancing the virtual clock alone can race ahead of
     * that real background hop. Interleave advancing the scheduler with short real sleeps until
     * the condition holds, bounded by a timeout so a genuine regression still fails fast instead
     * of hanging.
     */
    private fun settle(timeoutMs: Long = 5_000, condition: () -> Boolean) {
        dispatcher.scheduler.advanceUntilIdle()
        val deadline = System.currentTimeMillis() + timeoutMs
        while (!condition()) {
            check(System.currentTimeMillis() < deadline) {
                "Timed out waiting for the expected BookReaderViewModel/DataStore state."
            }
            Thread.sleep(5)
            dispatcher.scheduler.advanceUntilIdle()
        }
    }

    private fun awaitLoadFinished(viewModel: BookReaderViewModel, timeoutMs: Long = 5_000) {
        settle(timeoutMs) { !viewModel.state.value.isLoading }
    }

    private fun httpException(code: Int): HttpException =
        HttpException(Response.error<Any>(code, "".toResponseBody(null)))

    private fun epubResponseBody(): ResponseBody =
        epubBytes().toResponseBody("application/epub+zip".toMediaType())

    private fun epubBytes(): ByteArray {
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
                        <dc:identifier id="book-id">urn:uuid:mulletaflix-viewmodel-test</dc:identifier>
                        <dc:title>Integration Test Book</dc:title>
                        <dc:language>pt-BR</dc:language>
                      </metadata>
                      <manifest>
                        <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter-2" href="chapter-2.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="chapter"/><itemref idref="chapter-2"/></spine>
                    </package>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/chapter.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
                      <head><title>Capítulo</title></head>
                      <body><h1>Leitura funcionando</h1><p>Conteúdo EPUB mínimo válido.</p></body>
                    </html>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/chapter-2.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
                      <head><title>Capítulo dois</title></head>
                      <body><h1>Retomada funcionando</h1><p>Posição salva restaurada.</p></body>
                    </html>""".trimIndent(),
            )
        }
        return output.toByteArray()
    }

    private fun ZipOutputStream.writeEntry(path: String, content: String) {
        putNextEntry(ZipEntry(path))
        write(content.toByteArray(Charsets.UTF_8))
        closeEntry()
    }

    private companion object {
        val SCOPE_A = HomeFeedCacheScope("server-a", "https://server-a.example", "user-a")
        val SCOPE_B = HomeFeedCacheScope("server-b", "https://server-b.example", "user-b")
    }
}

private class GatedEpubResponseBody(
    private val delegate: ResponseBody,
    private val contentTypeRequested: CountDownLatch,
    private val allowContentType: CountDownLatch,
) : ResponseBody() {
    override fun contentType() = delegate.contentType().also {
        contentTypeRequested.countDown()
        check(allowContentType.await(5, TimeUnit.SECONDS))
    }

    override fun contentLength() = delegate.contentLength()

    override fun source(): BufferedSource = delegate.source()
}

/** Minimal [SessionRepository] test double; only the book reader's dependencies are wired. */
private class FakeSessionRepository(
    private val scopeProvider: () -> Flow<HomeFeedCacheScope?>,
) : SessionRepository {
    constructor(scope: HomeFeedCacheScope) : this({ flowOf(scope) })

    override fun getAccessToken(): Flow<String?> = flowOf(null)
    override fun getDeviceId(): Flow<String> = flowOf("device-test")
    override fun getBaseUrl(): Flow<String> = flowOf("https://server.example")
    override fun getCurrentUserId(): Flow<String?> = flowOf(null)
    override fun getHomeFeedCacheScope(): Flow<HomeFeedCacheScope?> = scopeProvider()
    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
    override suspend fun setBaseUrl(url: String) = Unit
    override suspend fun clearSession() = Unit
    override fun getSavedServers(): Flow<List<SavedServerSession>> = flowOf(emptyList())
}
