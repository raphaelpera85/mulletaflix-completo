package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.io.InputStream
import java.io.OutputStream
import java.nio.file.Files
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.Job
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class BookReaderPayloadPolicyTest {
    @Test
    fun `accepts epub pdf cbz and generic binary book content types`() {
        assertFalse(isClearlyNotSupportedBookContentType("application/epub+zip"))
        assertFalse(isClearlyNotSupportedBookContentType("application/pdf"))
        assertFalse(isClearlyNotSupportedBookContentType("application/x-cbz"))
        assertFalse(isClearlyNotSupportedBookContentType("application/vnd.comicbook+zip"))
        assertFalse(isClearlyNotSupportedBookContentType("application/vnd.comicbook-rar"))
        assertFalse(isClearlyNotSupportedBookContentType("application/x-cbr"))
        assertFalse(isClearlyNotSupportedBookContentType("application/zip"))
        assertTrue(PdfBookDocument.supports("Application/PDF; charset=binary"))
        assertFalse(PdfBookDocument.supports("application/epub+zip"))
    }

    @Test
    fun `accepts FictionBook XML and detects its root when MIME is generic`() {
        assertFalse(isClearlyNotSupportedBookContentType("application/x-fictionbook+xml"))
        assertFalse(isClearlyNotSupportedBookContentType("application/xml; charset=utf-8"))
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, bookReaderPayloadLimit("application/xml"))

        val directory = Files.createTempDirectory("fictionbook-sniff-test").toFile()
        try {
            val fictionBook = directory.resolve("book.bin").apply {
                writeText("<?xml version=\"1.0\"?><FictionBook><body><p>Texto</p></body></FictionBook>")
            }
            assertTrue(PlainTextBookDocument.hasFictionBookRoot(fictionBook))
            assertEquals(
                BookPayloadFormat.PLAIN_TEXT,
                detectBookPayloadFormat(fictionBook, "application/octet-stream"),
            )

            val otherXml = directory.resolve("other.xml").apply { writeText("<feed><entry/></feed>") }
            assertFalse(PlainTextBookDocument.hasFictionBookRoot(otherXml))
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun `accepts generic or missing content type for parser validation`() {
        assertFalse(isClearlyNotSupportedBookContentType("application/octet-stream"))
        assertFalse(isClearlyNotSupportedBookContentType("application/octet-stream; charset=binary"))
        assertFalse(isClearlyNotSupportedBookContentType(null))
        assertFalse(isClearlyNotSupportedBookContentType("  "))
    }

    @Test
    fun `rejects explicitly incompatible response types`() {
        assertFalse(isClearlyNotSupportedBookContentType("text/html; charset=utf-8"))
        assertTrue(isClearlyNotSupportedBookContentType("image/jpeg"))
        assertTrue(isClearlyNotSupportedBookContentType("application/json"))
        assertTrue(isClearlyNotSupportedBookContentType("application/x-cb7"))
        assertTrue(isClearlyNotSupportedBookContentType("application/x-mobipocket-ebook"))
    }

    @Test
    fun `uses a small streaming limit for direct text and general limit for converted books`() {
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, bookReaderPayloadLimit("text/plain; charset=utf-8"))
        assertEquals(PlainTextBookDocument.MAX_HTML_BYTES, bookReaderPayloadLimit("text/html; charset=utf-8"))
        assertEquals(MAX_BOOK_PAYLOAD_BYTES, bookReaderPayloadLimit("application/epub+zip"))
        assertEquals(MAX_BOOK_PAYLOAD_BYTES, bookReaderPayloadLimit(CbrBookArchive.CONTENT_TYPE))
        assertEquals(MAX_BOOK_PAYLOAD_BYTES, bookReaderPayloadLimit(null))
    }

    @Test
    fun `direct text stream stops at its limit without a known content length`() {
        val textLimit = bookReaderPayloadLimit("text/plain")
        val output = ByteArrayOutputStream()
        val oversizedPayload = ByteArray((textLimit + 1L).toInt())

        assertThrows(IOException::class.java) {
            copyBookReaderPayload(
                ByteArrayInputStream(oversizedPayload),
                output,
                maxBytes = textLimit,
            )
        }
        assertEquals(textLimit, output.size().toLong())
    }

    @Test
    fun `generic XML book stream is capped at the direct text limit`() {
        val payload = ByteArray(PlainTextBookDocument.MAX_TEXT_BYTES.toInt() + 1).apply {
            "<?xml version=\"1.0\"?><FictionBook>".toByteArray().copyInto(this)
        }
        val output = ByteArrayOutputStream()

        assertThrows(IOException::class.java) {
            copyBookReaderPayload(
                ByteArrayInputStream(payload),
                output,
                maxBytes = MAX_BOOK_PAYLOAD_BYTES,
                contentType = "application/octet-stream",
            )
        }
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, output.size().toLong())
    }

    @Test
    fun `generic XML stream with a long whitespace prologue stays capped`() {
        val prefix = " ".repeat(64 * 1024) + "<FictionBook>"
        val payload = ByteArray(PlainTextBookDocument.MAX_TEXT_BYTES.toInt() + 1).apply {
            prefix.toByteArray().copyInto(this)
        }
        val output = ByteArrayOutputStream()

        assertThrows(IOException::class.java) {
            copyBookReaderPayload(
                ByteArrayInputStream(payload),
                output,
                maxBytes = MAX_BOOK_PAYLOAD_BYTES,
                contentType = "application/octet-stream",
            )
        }
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, output.size().toLong())
    }

    @Test
    fun `generic XML stream with a long processing instruction stays capped`() {
        val prefix = "<?custom ${"x".repeat(64 * 1024)}?><FictionBook>"
        val payload = ByteArray(PlainTextBookDocument.MAX_TEXT_BYTES.toInt() + 1).apply {
            prefix.toByteArray().copyInto(this)
        }
        val output = ByteArrayOutputStream()

        assertThrows(IOException::class.java) {
            copyBookReaderPayload(
                ByteArrayInputStream(payload),
                output,
                maxBytes = MAX_BOOK_PAYLOAD_BYTES,
                contentType = "application/octet-stream",
            )
        }
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, output.size().toLong())
    }

    @Test
    fun `generic XML in UTF-16BE without BOM stays capped`() {
        val prefix = "<?xml version=\"1.0\"?><FictionBook>".toByteArray(Charsets.UTF_16BE)
        val payload = ByteArray(PlainTextBookDocument.MAX_TEXT_BYTES.toInt() + 1).apply {
            prefix.copyInto(this)
        }
        val output = ByteArrayOutputStream()

        assertThrows(IOException::class.java) {
            copyBookReaderPayload(
                ByteArrayInputStream(payload),
                output,
                maxBytes = MAX_BOOK_PAYLOAD_BYTES,
                contentType = "application/octet-stream",
            )
        }
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, output.size().toLong())
    }

    @Test
    fun `generic PDF and ZIP signatures retain the large book limit`() {
        val headers = listOf(
            "%PDF-".toByteArray(Charsets.US_ASCII),
            byteArrayOf('P'.code.toByte(), 'K'.code.toByte(), 0x03, 0x04, 0),
        )

        headers.forEach { header ->
            val payload = ByteArray(PlainTextBookDocument.MAX_TEXT_BYTES.toInt() + 1).apply {
                header.copyInto(this)
            }
            val output = ByteArrayOutputStream()
            val copied = copyBookReaderPayload(
                ByteArrayInputStream(payload),
                output,
                maxBytes = MAX_BOOK_PAYLOAD_BYTES,
                contentType = "application/octet-stream",
            )

            assertEquals(payload.size.toLong(), copied)
            assertEquals(payload.size, output.size())
        }
    }

    @Test
    fun `DOCX MIME and ZIP signature allow payloads larger than plain text limit`() {
        val payload = ByteArray(PlainTextBookDocument.MAX_TEXT_BYTES.toInt() + 1).apply {
            this[0] = 'P'.code.toByte()
            this[1] = 'K'.code.toByte()
            this[2] = 0x03
            this[3] = 0x04
        }
        val output = ByteArrayOutputStream()

        val copied = copyBookReaderPayload(
            ByteArrayInputStream(payload),
            output,
            maxBytes = bookReaderPayloadLimit(DocxBookTextExtractor.CONTENT_TYPE),
            contentType = DocxBookTextExtractor.CONTENT_TYPE,
        )

        assertEquals(payload.size.toLong(), copied)
        assertEquals(payload.size, output.size())
    }

    @Test
    fun `generic ODT ZIP is identified from its package prefix and capped while streaming`() {
        val prefix = odtPackagePrefix()
        val totalBytes = OdtBookTextExtractor.MAX_PACKAGE_BYTES + 1L
        val input = object : InputStream() {
            private var position = 0L

            override fun read(): Int {
                if (position >= totalBytes) return -1
                val value = if (position < prefix.size) prefix[position.toInt()].toInt() and 0xFF else 0
                position++
                return value
            }

            override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
                if (position >= totalBytes) return -1
                val count = minOf(length.toLong(), totalBytes - position).toInt()
                for (index in 0 until count) {
                    buffer[offset + index] = if (position + index < prefix.size) {
                        prefix[(position + index).toInt()]
                    } else {
                        0
                    }
                }
                position += count
                return count
            }
        }
        val output = object : OutputStream() {
            var bytesWritten = 0L
                private set

            override fun write(value: Int) {
                bytesWritten++
            }

            override fun write(buffer: ByteArray, offset: Int, length: Int) {
                bytesWritten += length
            }
        }

        assertThrows(IOException::class.java) {
            copyBookReaderPayload(input, output, maxBytes = MAX_BOOK_PAYLOAD_BYTES, contentType = "application/octet-stream")
        }
        assertEquals(OdtBookTextExtractor.MAX_PACKAGE_BYTES, output.bytesWritten)
    }

    @Test
    fun `detects PDF and CBZ signatures when MIME is generic`() {
        val directory = Files.createTempDirectory("book-format-sniff-test").toFile()
        try {
            val pdf = directory.resolve("payload.bin").apply { writeBytes("%PDF-1.7".toByteArray()) }
            assertEquals(BookPayloadFormat.PDF, detectBookPayloadFormat(pdf, "application/octet-stream"))

            val cbz = directory.resolve("comic.bin")
            ZipOutputStream(cbz.outputStream()).use { zip ->
                zip.putNextEntry(ZipEntry("page-001.jpg"))
                zip.write(byteArrayOf(1, 2, 3))
                zip.closeEntry()
            }
            assertEquals(BookPayloadFormat.CBZ, detectBookPayloadFormat(cbz, "application/octet-stream"))

            val cbr = directory.resolve("comic.rar").apply {
                writeBytes(byteArrayOf(0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x00))
            }
            assertEquals(BookPayloadFormat.CBR, detectBookPayloadFormat(cbr, "application/octet-stream"))

            val epub = directory.resolve("book.bin")
            ZipOutputStream(epub.outputStream()).use { zip ->
                zip.putNextEntry(ZipEntry("META-INF/container.xml"))
                zip.write("<container/>".toByteArray())
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("cover.jpg"))
                zip.write(byteArrayOf(1, 2, 3))
                zip.closeEntry()
            }
            assertEquals(BookPayloadFormat.EPUB, detectBookPayloadFormat(epub, "application/octet-stream"))
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun `generic RAR signatures retain the large book transfer limit`() {
        val payload = ByteArray(PlainTextBookDocument.MAX_TEXT_BYTES.toInt() + 1).apply {
            "Rar!\u001a\u0007".toByteArray(Charsets.ISO_8859_1).copyInto(this)
        }
        val output = ByteArrayOutputStream()

        assertEquals(
            payload.size.toLong(),
            copyBookReaderPayload(
                ByteArrayInputStream(payload),
                output,
                contentType = "application/octet-stream",
            ),
        )
        assertEquals(payload.size, output.size())
    }

    private fun odtPackagePrefix(): ByteArray {
        val mimeBytes = OdtBookTextExtractor.CONTENT_TYPE.toByteArray(Charsets.US_ASCII)
        val output = ByteArrayOutputStream()
        ZipOutputStream(output).use { zip ->
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
        }
        return output.toByteArray().copyOf(OdtBookTextExtractor.ZIP_MIMETYPE_PREFIX_BYTES)
    }


    @Test
    fun `recognizes standard and server comic archive content types`() {
        assertTrue(ComicBookArchive.supports("application/x-cbz"))
        assertTrue(ComicBookArchive.supports("Application/X-CBZ; charset=binary"))
        assertTrue(ComicBookArchive.supports("application/vnd.comicbook+zip"))
        assertFalse(ComicBookArchive.supports("application/epub+zip"))
    }

    @Test
    fun `book payload copy enforces byte limit while streaming`() {
        val boundaryOutput = ByteArrayOutputStream()
        assertEquals(
            3L,
            copyBookReaderPayload(ByteArrayInputStream(byteArrayOf(1, 2, 3)), boundaryOutput, maxBytes = 3),
        )
        assertEquals(3, boundaryOutput.size())

        val output = ByteArrayOutputStream()

        val failure = assertThrows(IOException::class.java) {
            copyBookReaderPayload(ByteArrayInputStream(byteArrayOf(1, 2, 3, 4)), output, maxBytes = 3)
        }

        assertTrue(failure.message.orEmpty().contains("size limit"))
        assertTrue(output.size() <= 3)
    }

    @Test
    fun `maps unsupported conversion response to actionable message`() {
        assertEquals(
            "Este formato não pode ser lido pelo aplicativo.",
            bookReaderHttpFailureMessage(415),
        )
    }

    @Test
    fun `includes unexpected server response code in error`() {
        assertEquals(
            "Não foi possível carregar o livro (HTTP 503).",
            bookReaderHttpFailureMessage(503),
        )
    }

    @Test
    fun `reader cleanup removes only files owned by that reader`() {
        val directory = Files.createTempDirectory("book-reader-cache-test").toFile()
        try {
            val outsideFile = directory.resolve("other-cache-entry.epub").apply { writeText("keep") }
            val firstReader = BookReaderCacheFiles(directory)
            val secondReader = BookReaderCacheFiles(directory)
            val firstBook = firstReader.create()
            val comicBook = firstReader.create(comicArchive = true)
            val secondBook = secondReader.create()

            firstReader.deleteAllOwned()

            assertTrue(outsideFile.exists())
            assertFalse(firstBook.exists())
            assertFalse(comicBook.exists())
            assertTrue(comicBook.name.endsWith(".cbz"))
            assertTrue(secondBook.exists())

            val replacement = secondReader.create(comicArchive = true)
            secondReader.deleteAllOwnedExcept(replacement)
            assertFalse(secondBook.exists())
            assertTrue(replacement.exists())
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun `reader startup recovers orphaned books without deleting active or unrelated files`() {
        val directory = Files.createTempDirectory("book-reader-recovery-test").toFile()
        try {
            val orphanedEpub = directory.resolve("book-reader-crashed.epub").apply { writeText("partial") }
            val orphanedCbz = directory.resolve("book-reader-crashed.cbz").apply { writeText("partial") }
            val orphanedPdf = directory.resolve("book-reader-crashed.pdf").apply { writeText("partial") }
            val unrelatedBook = directory.resolve("user-book.epub").apply { writeText("keep") }
            val unrelatedTemp = directory.resolve("book-reader-crashed.tmp").apply { writeText("keep") }

            val firstReader = BookReaderCacheFiles(directory)

            assertFalse(orphanedEpub.exists())
            assertFalse(orphanedCbz.exists())
            assertFalse(orphanedPdf.exists())
            assertTrue(unrelatedBook.exists())
            assertTrue(unrelatedTemp.exists())

            val activeBook = firstReader.create()
            BookReaderCacheFiles(directory)

            assertTrue("A second reader initialization deleted an active file.", activeBook.exists())
            firstReader.deleteAllOwned()
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun `failed reader cache deletion is retried by a later reader initialization`() {
        val directory = Files.createTempDirectory("book-reader-delete-retry-test").toFile()
        try {
            val reader = BookReaderCacheFiles(directory) { false }
            val book = reader.create()

            reader.delete(book)

            assertTrue(book.exists())
            BookReaderCacheFiles(directory)
            assertFalse(book.exists())
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun `reader cache deletion security exception does not block later recovery`() {
        val directory = Files.createTempDirectory("book-reader-delete-exception-test").toFile()
        try {
            val reader = BookReaderCacheFiles(directory) { throw SecurityException("temporary denial") }
            val book = reader.create()

            reader.delete(book)

            assertTrue(book.exists())
            BookReaderCacheFiles(directory)
            assertFalse(book.exists())
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun `reader cleanup waits until an active parse job completes`() {
        val directory = Files.createTempDirectory("book-reader-parse-test").toFile()
        try {
            val cache = BookReaderCacheFiles(directory)
            val bookFile = cache.create()
            val parseJob = Job()

            cache.deleteAfter(parseJob)

            assertTrue(bookFile.exists())
            parseJob.complete()
            assertFalse(bookFile.exists())
        } finally {
            directory.deleteRecursively()
        }
    }
}
