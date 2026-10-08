package org.mulletaflix.feature.itemdetail

import java.io.File
import java.io.IOException
import java.nio.file.Files
import java.util.zip.CRC32
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
class OdtBookTextExtractorTest {
    @Test
    fun `reads ODT paragraphs headings spaces line breaks and table cells`() = withTempDirectory { directory ->
        val file = directory.resolve("book.odt")
        writeOdt(
            file,
            """<office:document-content xmlns:office="$OFFICE_NS" xmlns:text="$TEXT_NS" xmlns:table="$TABLE_NS">
                <office:body><office:text>
                  <text:h>Capítulo 1</text:h>
                  <text:p>Olá <text:span>mundo</text:span><text:s text:c="2"/>com espaços<text:tab/>tabulado<text:line-break/>linha final.</text:p>
                  <table:table><table:table-row><table:table-cell><text:p>Coluna A</text:p></table:table-cell><table:table-cell><text:p>Coluna B</text:p></table:table-cell></table:table-row></table:table>
                </office:text></office:body>
              </office:document-content>""".trimIndent(),
            includeImage = true,
        )

        assertTrue(PlainTextBookDocument.supports("${OdtBookTextExtractor.CONTENT_TYPE}; charset=binary"))
        assertEquals(OdtBookTextExtractor.MAX_PACKAGE_BYTES, bookReaderPayloadLimit(OdtBookTextExtractor.CONTENT_TYPE))
        assertTrue(OdtBookTextExtractor.hasOpenDocumentTextPackage(file))
        assertEquals(
            BookPayloadFormat.PLAIN_TEXT,
            detectBookPayloadFormat(file, "application/octet-stream"),
        )

        val text = PlainTextBookDocument.open(file, "application/octet-stream").chunks.joinToString("")
        assertTrue(text.contains("Capítulo 1"))
        assertTrue(text.contains("Olá mundo  com espaços\ttabulado\nlinha final."))
        assertTrue(text.contains("Coluna A\tColuna B"))
        assertFalse(text.contains("image-data"))
    }

    @Test
    fun `rejects malformed packages DTDs wrong MIME and oversized packages`() = withTempDirectory { directory ->
        val invalidZip = directory.resolve("invalid.odt").apply { writeText("not a ZIP") }
        assertThrows(IOException::class.java) { OdtBookTextExtractor.extract(invalidZip) }

        val wrongMime = directory.resolve("wrong-mime.odt")
        writeOdt(wrongMime, "<office:document-content xmlns:office=\"$OFFICE_NS\"/>", mimeType = "application/zip")
        assertThrows(IOException::class.java) { OdtBookTextExtractor.extract(wrongMime) }

        val unsafe = directory.resolve("unsafe.odt")
        writeOdt(
            unsafe,
            """<!DOCTYPE office:document-content [<!ENTITY secret SYSTEM "file:///etc/passwd">]>
                <office:document-content xmlns:office="$OFFICE_NS" xmlns:text="$TEXT_NS">
                  <office:body><office:text><text:p>&secret;</text:p></office:text></office:body>
                </office:document-content>""".trimIndent(),
        )
        assertThrows(IOException::class.java) { OdtBookTextExtractor.extract(unsafe) }

        val oversized = directory.resolve("oversized.odt").apply {
            java.io.RandomAccessFile(this, "rw").use { it.setLength(OdtBookTextExtractor.MAX_PACKAGE_BYTES + 1L) }
        }
        assertThrows(IOException::class.java) { OdtBookTextExtractor.extract(oversized) }
    }

    private fun writeOdt(
        file: File,
        contentXml: String,
        includeImage: Boolean = false,
        mimeType: String = OdtBookTextExtractor.CONTENT_TYPE,
    ) {
        ZipOutputStream(file.outputStream()).use { zip ->
            val mimeBytes = mimeType.toByteArray(Charsets.US_ASCII)
            val mimeEntry = ZipEntry("mimetype").apply {
                method = ZipEntry.STORED
                size = mimeBytes.size.toLong()
                compressedSize = mimeBytes.size.toLong()
                crc = CRC32().apply { update(mimeBytes) }.value
            }
            zip.putNextEntry(mimeEntry)
            zip.write(mimeBytes)
            zip.closeEntry()

            zip.putNextEntry(ZipEntry("content.xml"))
            zip.write(contentXml.toByteArray(Charsets.UTF_8))
            zip.closeEntry()

            if (includeImage) {
                zip.putNextEntry(ZipEntry("Pictures/cover.jpg"))
                zip.write("image-data".toByteArray())
                zip.closeEntry()
            }
        }
    }

    private inline fun withTempDirectory(block: (File) -> Unit) {
        val directory = Files.createTempDirectory("odt-book-test").toFile()
        try {
            block(directory)
        } finally {
            directory.deleteRecursively()
        }
    }

    private companion object {
        const val OFFICE_NS = "urn:oasis:names:tc:opendocument:xmlns:office:1.0"
        const val TEXT_NS = "urn:oasis:names:tc:opendocument:xmlns:text:1.0"
        const val TABLE_NS = "urn:oasis:names:tc:opendocument:xmlns:table:1.0"
    }
}
