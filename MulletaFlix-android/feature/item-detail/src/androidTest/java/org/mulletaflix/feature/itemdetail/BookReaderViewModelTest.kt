package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.ViewModelStore
import androidx.lifecycle.ViewModelStoreOwner
import androidx.test.core.app.ApplicationProvider
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.io.InputStream
import java.io.File
import java.lang.reflect.Proxy
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody
import okhttp3.ResponseBody.Companion.toResponseBody
import okio.BufferedSource
import okio.buffer
import okio.source
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import retrofit2.Response

class BookReaderViewModelTest {

    private val context: Context
        get() = ApplicationProvider.getApplicationContext()

    @Test
    fun validEpubOpensAndRetryReleasesCachedBook() = runBlocking {
        val cacheBefore = readerCacheFiles()
        val responses = ArrayDeque<Response<ResponseBody>>().apply {
            add(Response.success(epubBody(minimalEpub())))
            add(Response.error(404, "missing".toResponseBody("text/plain".toMediaType())))
        }
        val (viewModel, store) = viewModel { responses.removeFirst() }

        try {
            viewModel.load("book-1")
            val loaded = awaitTerminalState(viewModel)
            assertNotNull(loaded.publication)
            assertNull(loaded.errorMessage)
            assertEquals(cacheBefore.size + 1, readerCacheFiles().size)

            viewModel.retry("book-1")
            val failed = awaitTerminalState(viewModel) { it.errorMessage != null }
            assertNull(failed.publication)
            assertTrue(failed.errorMessage.orEmpty().contains("não foi encontrado"))
            assertEquals(cacheBefore, readerCacheFiles())
        } finally {
            store.clear()
            readerCacheFiles().minus(cacheBefore).forEach { it.delete() }
        }
    }

    @Test
    fun initializationRemovesEpubLeftByPreviousProcess() {
        val cacheDir = File(context.cacheDir, BOOK_READER_CACHE_DIR).apply { mkdirs() }
        val staleBook = File(cacheDir, "mulletaflix-book-stale.epub").apply {
            writeText("stale")
        }

        val (_, store) = viewModel {
            error("download não deve ser iniciado durante a limpeza")
        }

        try {
            assertFalse(staleBook.exists())
        } finally {
            store.clear()
        }
    }

    @Test
    fun emptyEpubFailsWithoutLeavingTemporaryFile() = runBlocking {
        val cacheBefore = readerCacheFiles()
        val (viewModel, store) = viewModel {
            Response.success(epubBody(ByteArray(0)))
        }

        try {
            viewModel.load("empty-book")
            val failed = awaitTerminalState(viewModel)
            assertTrue(failed.errorMessage.orEmpty().contains("vazio"))
            assertEquals(cacheBefore, readerCacheFiles())
        } finally {
            store.clear()
            readerCacheFiles().minus(cacheBefore).forEach { it.delete() }
        }
    }

    @Test
    fun mimeMismatchIsRejectedBeforeCreatingTemporaryFile() = runBlocking {
        val cacheBefore = readerCacheFiles()
        val (viewModel, store) = viewModel {
            Response.success("not-an-epub".toResponseBody("application/pdf".toMediaType()))
        }

        try {
            viewModel.load("pdf-book")
            val failed = awaitTerminalState(viewModel)
            assertTrue(failed.errorMessage.orEmpty().contains("incompatível"))
            assertEquals(cacheBefore, readerCacheFiles())
        } finally {
            store.clear()
            readerCacheFiles().minus(cacheBefore).forEach { it.delete() }
        }
    }

    @Test
    fun httpReaderErrorsAreSurfacedWithoutLeavingTemporaryFiles() = runBlocking {
        val cases = listOf(
            Triple(401, "book-401", "sessão"),
            Triple(403, "book-403", "autorização"),
            Triple(404, "book-404", "não foi encontrado"),
            Triple(415, "book-415", "formato"),
        )
        cases.forEach { (code, itemId, expectedMessage) ->
            val (viewModel, store) = viewModel {
                Response.error(code, "error-$code".toResponseBody("text/plain".toMediaType()))
            }
            val cacheBefore = readerCacheFiles()

            try {
                viewModel.load(itemId)
                val failed = awaitTerminalState(viewModel) {
                    !it.isLoading && it.errorMessage.orEmpty().contains(expectedMessage)
                }
                assertNull(failed.publication)
                assertEquals(cacheBefore, readerCacheFiles())
            } finally {
                store.clear()
                readerCacheFiles().minus(cacheBefore).forEach { it.delete() }
            }
        }
    }

    @Test
    fun interruptedDownloadFailsAndRemovesPartialTemporaryFile() = runBlocking {
        val (viewModel, store) = viewModel {
            Response.success(InterruptedResponseBody())
        }
        val cacheBefore = readerCacheFiles()

        try {
            viewModel.load("interrupted-book")
            val failed = awaitTerminalState(viewModel)

            assertNull(failed.publication)
            assertTrue(failed.errorMessage.orEmpty().contains("rede interrompida"))
            assertEquals(cacheBefore, readerCacheFiles())
        } finally {
            store.clear()
            readerCacheFiles().minus(cacheBefore).forEach { it.delete() }
        }
    }

    @Test
    fun retryCancelsBlockedStreamClosesBodyAndRemovesTemporaryFile() = runBlocking {
        val cacheBefore = readerCacheFiles()
        val blockedBody = BlockingResponseBody()
        val responses = ArrayDeque<Response<ResponseBody>>().apply {
            add(Response.success(blockedBody))
            add(Response.error(404, "missing".toResponseBody("text/plain".toMediaType())))
        }
        val (viewModel, store) = viewModel { responses.removeFirst() }

        try {
            viewModel.load("blocked-book")
            assertTrue(
                "o download deve iniciar a leitura do stream",
                withContext(Dispatchers.IO) { blockedBody.awaitReadStarted() },
            )
            assertEquals(cacheBefore.size + 1, readerCacheFiles().size)

            viewModel.retry("blocked-book")
            val failed = awaitTerminalState(viewModel) { it.errorMessage != null }

            assertTrue(blockedBody.wasClosed)
            assertTrue(failed.errorMessage.orEmpty().contains("não foi encontrado"))
            assertEquals(cacheBefore, readerCacheFiles())
        } finally {
            store.clear()
            readerCacheFiles().minus(cacheBefore).forEach { it.delete() }
        }
    }

    @Test
    fun clearingViewModelCancelsBlockedStreamAndCleansTemporaryFile() = runBlocking {
        val cacheBefore = readerCacheFiles()
        val blockedBody = BlockingResponseBody()
        val (viewModel, store) = viewModel { Response.success(blockedBody) }

        try {
            viewModel.load("blocked-on-clear")
            assertTrue(
                "o download deve iniciar a leitura do stream",
                withContext(Dispatchers.IO) { blockedBody.awaitReadStarted() },
            )
            assertEquals(cacheBefore.size + 1, readerCacheFiles().size)

            store.clear()

            assertTrue(
                "cancelar o ViewModel deve fechar o body bloqueado",
                withContext(Dispatchers.IO) { blockedBody.awaitClosed() },
            )
            assertEquals(cacheBefore, readerCacheFiles())
        } finally {
            store.clear()
            readerCacheFiles().minus(cacheBefore).forEach { it.delete() }
        }
    }

    private fun viewModel(
        responseProvider: () -> Response<ResponseBody>,
    ): Pair<BookReaderViewModel, ViewModelStore> {
        val api = Proxy.newProxyInstance(
            MulletaFlixApiService::class.java.classLoader,
            arrayOf(MulletaFlixApiService::class.java),
        ) { proxy, method, args ->
            when (method.name) {
                "getBookEpub" -> responseProvider()
                "toString" -> "BookReaderTestApi"
                "hashCode" -> System.identityHashCode(proxy)
                "equals" -> proxy === args?.firstOrNull()
                else -> error("Unexpected API call: ${method.name}")
            }
        } as MulletaFlixApiService

        val store = ViewModelStore()
        val owner = object : ViewModelStoreOwner {
            override val viewModelStore: ViewModelStore = store
        }
        val factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T =
                BookReaderViewModel(context, api) as T
        }
        return ViewModelProvider(owner, factory)[BookReaderViewModel::class.java] to store
    }

    private suspend fun awaitTerminalState(
        viewModel: BookReaderViewModel,
        predicate: (BookReaderUiState) -> Boolean = {
            it.publication != null || it.errorMessage != null
        },
    ): BookReaderUiState = withTimeout(15_000) {
        viewModel.state.first(predicate)
    }

    private fun readerCacheFiles(): Set<java.io.File> =
        File(context.cacheDir, BOOK_READER_CACHE_DIR).listFiles()
            .orEmpty()
            .filter { it.name.startsWith("mulletaflix-book-") && it.name.endsWith(".epub") }
            .toSet()

    private fun epubBody(bytes: ByteArray): ResponseBody =
        bytes.toResponseBody("application/epub+zip".toMediaType())

    private fun minimalEpub(): ByteArray {
        val output = ByteArrayOutputStream()
        ZipOutputStream(output).use { zip ->
            val mimetype = "application/epub+zip".toByteArray()
            val crc = CRC32().apply { update(mimetype) }
            zip.putNextEntry(ZipEntry("mimetype").apply {
                method = ZipEntry.STORED
                size = mimetype.size.toLong()
                compressedSize = mimetype.size.toLong()
                this.crc = crc.value
            })
            zip.write(mimetype)
            zip.closeEntry()

            zip.writeEntry(
                "META-INF/container.xml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                      <rootfiles>
                        <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                      </rootfiles>
                    </container>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/content.opf",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <package version="3.0" unique-identifier="pub-id" xmlns="http://www.idpf.org/2007/opf">
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:identifier id="pub-id">urn:uuid:mulletaflix-reader-test</dc:identifier>
                        <dc:title>Livro de teste</dc:title>
                        <dc:language>pt-BR</dc:language>
                        <meta property="dcterms:modified">2026-10-02T00:00:00Z</meta>
                      </metadata>
                      <manifest>
                        <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                        <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="chapter"/></spine>
                    </package>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/nav.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
                      <head><title>Sumário</title></head>
                      <body><nav epub:type="toc"><ol><li><a href="chapter.xhtml">Capítulo</a></li></ol></nav></body>
                    </html>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/chapter.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml">
                      <head><title>Capítulo</title></head>
                      <body><h1>Capítulo de teste</h1><p>Conteúdo local do leitor.</p></body>
                    </html>
                """.trimIndent(),
            )
        }
        return output.toByteArray()
    }

    private fun ZipOutputStream.writeEntry(name: String, contents: String) {
        putNextEntry(ZipEntry(name))
        write(contents.toByteArray())
        closeEntry()
    }

    private class BlockingResponseBody : ResponseBody() {
        private val input = BlockingInputStream()
        private val bufferedSource = input.source().buffer()

        val wasClosed: Boolean
            get() = input.wasClosed

        override fun contentType() = "application/epub+zip".toMediaType()

        override fun contentLength(): Long = -1L

        override fun source(): BufferedSource = bufferedSource

        fun awaitReadStarted(): Boolean = input.readStarted.await(5, TimeUnit.SECONDS)

        fun awaitClosed(): Boolean = input.closed.await(5, TimeUnit.SECONDS)
    }

    private class InterruptedResponseBody : ResponseBody() {
        private val input = InterruptedInputStream()
        private val bufferedSource = input.source().buffer()

        override fun contentType() = "application/epub+zip".toMediaType()

        override fun contentLength(): Long = -1L

        override fun source(): BufferedSource = bufferedSource
    }

    private class InterruptedInputStream : InputStream() {
        private var emittedPrefix = false

        override fun read(): Int {
            if (emittedPrefix) throw IOException("rede interrompida")
            emittedPrefix = true
            return 'P'.code
        }

        override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
            if (emittedPrefix) throw IOException("rede interrompida")
            val prefix = byteArrayOf('P'.code.toByte(), 'K'.code.toByte())
            val count = minOf(length, prefix.size)
            prefix.copyInto(buffer, destinationOffset = offset, endIndex = count)
            emittedPrefix = true
            return count
        }
    }

    private class BlockingInputStream : InputStream() {
        val readStarted = CountDownLatch(1)
        val closed = CountDownLatch(1)
        private val closedFlag = AtomicBoolean(false)

        val wasClosed: Boolean
            get() = closedFlag.get()

        override fun read(): Int {
            waitUntilClosed()
            return -1
        }

        override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
            waitUntilClosed()
            return -1
        }

        override fun close() {
            if (closedFlag.compareAndSet(false, true)) {
                closed.countDown()
            }
        }

        private fun waitUntilClosed() {
            readStarted.countDown()
            while (!closedFlag.get()) {
                try {
                    closed.await()
                } catch (interrupted: InterruptedException) {
                    Thread.currentThread().interrupt()
                    throw IOException("stream interrompido", interrupted)
                }
            }
            throw IOException("stream fechado")
        }
    }
}
