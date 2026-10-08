package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.nio.file.Files
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.Job
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class BookReaderPayloadPolicyTest {
    @Test
    fun `accepts epub pdf cbz and generic binary book content types`() {
        assertFalse(isClearlyNotSupportedBookContentType("application/epub+zip"))
        assertFalse(isClearlyNotSupportedBookContentType("application/pdf"))
        assertFalse(isClearlyNotSupportedBookContentType("application/x-cbz"))
        assertFalse(isClearlyNotSupportedBookContentType("application/vnd.comicbook+zip"))
        assertFalse(isClearlyNotSupportedBookContentType("application/zip"))
        assertTrue(PdfBookDocument.supports("Application/PDF; charset=binary"))
        assertFalse(PdfBookDocument.supports("application/epub+zip"))
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
        assertTrue(isClearlyNotSupportedBookContentType("text/html; charset=utf-8"))
        assertTrue(isClearlyNotSupportedBookContentType("image/jpeg"))
        assertTrue(isClearlyNotSupportedBookContentType("application/json"))
        assertTrue(isClearlyNotSupportedBookContentType("application/x-cbr"))
        assertTrue(isClearlyNotSupportedBookContentType("application/x-mobipocket-ebook"))
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
