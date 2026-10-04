package org.mulletaflix.feature.itemdetail

import java.nio.file.Files
import kotlinx.coroutines.Job
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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
            val secondBook = secondReader.create()

            firstReader.deleteAllOwned()

            assertTrue(outsideFile.exists())
            assertFalse(firstBook.exists())
            assertTrue(secondBook.exists())
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
