package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.nio.file.Files
import kotlinx.coroutines.Job
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class BookReaderPayloadPolicyTest {
    @Test
    fun `accepts standard epub content type`() {
        assertFalse(isClearlyNotEpubContentType("application/epub+zip"))
    }

    @Test
    fun `accepts generic or missing content type for parser validation`() {
        assertFalse(isClearlyNotEpubContentType("application/octet-stream"))
        assertFalse(isClearlyNotEpubContentType("application/octet-stream; charset=binary"))
        assertFalse(isClearlyNotEpubContentType(null))
        assertFalse(isClearlyNotEpubContentType("  "))
    }

    @Test
    fun `rejects explicitly incompatible response types`() {
        assertTrue(isClearlyNotEpubContentType("text/html; charset=utf-8"))
        assertTrue(isClearlyNotEpubContentType("image/jpeg"))
        assertTrue(isClearlyNotEpubContentType("application/pdf"))
        assertTrue(isClearlyNotEpubContentType("application/x-cbz"))
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
