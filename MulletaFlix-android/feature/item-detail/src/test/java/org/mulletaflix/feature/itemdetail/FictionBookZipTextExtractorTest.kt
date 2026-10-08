package org.mulletaflix.feature.itemdetail

import java.io.File
import java.io.IOException
import java.io.RandomAccessFile
import java.nio.file.Files
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
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
class FictionBookZipTextExtractorTest {
    @Test
    fun `reads FB2 from a generic ZIP package and classifies it before comic pages`() = withTempDirectory { directory ->
        val file = directory.resolve("book.fb2.zip")
        writeZip(
            file,
            "covers/cover.jpg" to byteArrayOf(1, 2, 3),
            "nested/book.FB2" to fictionBookXml("Texto FictionBook compactado."),
        )

        assertTrue(FictionBookZipTextExtractor.hasFictionBookPackage(file))
        assertEquals(
            BookPayloadFormat.PLAIN_TEXT,
            detectBookPayloadFormat(file, "application/octet-stream"),
        )
        val text = PlainTextBookDocument.open(file, "application/octet-stream").chunks.joinToString("")
        assertTrue(text.contains("Texto FictionBook compactado."))
        assertEquals(
            FictionBookZipTextExtractor.MAX_PACKAGE_BYTES,
            bookReaderPayloadLimit(FictionBookZipTextExtractor.CONTENT_TYPE),
        )
    }

    @Test
    fun `supports FBZ through its specific MIME and ignores archive cover images`() = withTempDirectory { directory ->
        val file = directory.resolve("book.fbz")
        writeZip(
            file,
            "book.fb2" to fictionBookXml("Texto FBZ."),
            "cover.png" to byteArrayOf(4, 5, 6),
        )

        assertTrue(FictionBookZipTextExtractor.supports("${FictionBookZipTextExtractor.CONTENT_TYPE}; name=book.fbz"))
        assertEquals(BookPayloadFormat.PLAIN_TEXT, detectBookPayloadFormat(file, FictionBookZipTextExtractor.CONTENT_TYPE))
        assertTrue(PlainTextBookDocument.open(file, FictionBookZipTextExtractor.CONTENT_TYPE).chunks.joinToString("").contains("Texto FBZ."))
    }

    @Test
    fun `rejects archives without exactly one FB2 document`() = withTempDirectory { directory ->
        val noBook = directory.resolve("images.zip")
        writeZip(noBook, "cover.jpg" to byteArrayOf(1, 2, 3))
        assertFalse(FictionBookZipTextExtractor.hasFictionBookPackage(noBook))

        val twoBooks = directory.resolve("two-books.zip")
        writeZip(
            twoBooks,
            "first.fb2" to fictionBookXml("Primeiro"),
            "second.fb2" to fictionBookXml("Segundo"),
        )
        assertFalse(FictionBookZipTextExtractor.hasFictionBookPackage(twoBooks))
        assertThrows(IOException::class.java) { FictionBookZipTextExtractor.extractXml(twoBooks) }
    }

    @Test
    fun `enforces decompressed XML limit even when ZIP package is small`() = withTempDirectory { directory ->
        val largeXml = fictionBookXml("x".repeat(FictionBookZipTextExtractor.MAX_XML_BYTES.toInt()))
        val file = directory.resolve("expanded.fb2.zip")
        writeZip(file, "book.fb2" to largeXml)

        assertTrue(file.length() < FictionBookZipTextExtractor.MAX_PACKAGE_BYTES)
        assertThrows(IOException::class.java) { FictionBookZipTextExtractor.extractXml(file) }
    }

    @Test
    fun `rejects package larger than the direct archive limit before opening ZIP`() = withTempDirectory { directory ->
        val file = directory.resolve("oversized.fb2.zip")
        RandomAccessFile(file, "rw").use { it.setLength(FictionBookZipTextExtractor.MAX_PACKAGE_BYTES + 1) }

        assertFalse(FictionBookZipTextExtractor.hasFictionBookPackage(file))
        assertThrows(IOException::class.java) { FictionBookZipTextExtractor.extractXml(file) }
    }

    @Test
    fun `rejects external entity references in an FB2 archive`() = withTempDirectory { directory ->
        val file = directory.resolve("unsafe.fb2.zip")
        writeZip(
            file,
            "unsafe.fb2" to """<!DOCTYPE FictionBook [<!ENTITY secret SYSTEM "file:///etc/passwd">]><FictionBook><body><p>&secret;</p></body></FictionBook>""".toByteArray(),
        )

        assertThrows(IOException::class.java) {
            PlainTextBookDocument.open(file, FictionBookZipTextExtractor.CONTENT_TYPE)
        }
    }

    private fun fictionBookXml(body: String): ByteArray =
        """<?xml version="1.0" encoding="UTF-8"?><FictionBook><body><section><p>$body</p></section></body></FictionBook>"""
            .toByteArray(Charsets.UTF_8)

    private fun writeZip(file: File, vararg entries: Pair<String, ByteArray>) {
        ZipOutputStream(file.outputStream()).use { zip ->
            entries.forEach { (name, content) ->
                zip.putNextEntry(ZipEntry(name))
                zip.write(content)
                zip.closeEntry()
            }
        }
    }

    private fun withTempDirectory(block: (File) -> Unit) {
        val directory = Files.createTempDirectory("fictionbook-zip-test").toFile()
        try {
            block(directory)
        } finally {
            directory.deleteRecursively()
        }
    }
}
