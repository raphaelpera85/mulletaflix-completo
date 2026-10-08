package org.mulletaflix.feature.itemdetail

import java.io.File
import java.io.IOException
import java.nio.file.Files
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class RtfBookTextParserTest {
    @Test
    fun `opens RTF MIME and strips formatting and metadata groups`() = withTempDirectory { directory ->
        val file = directory.resolve("book.rtf").apply {
            writeText(
                """{\rtf1\ansi\uc1 Ol\'e1 \b mundo\b0.\par Próxima linha: \u8217? ok. """ +
                    """{\*\generator hidden metadata} \{chaves\}.}""",
            )
        }

        assertEquals(
            BookPayloadFormat.PLAIN_TEXT,
            detectBookPayloadFormat(file, "application/rtf"),
        )
        assertEquals(
            "Olá mundo.\nPróxima linha: ’ ok.  {chaves}.",
            PlainTextBookDocument.open(file, "application/rtf").chunks.joinToString(""),
        )
    }

    @Test
    fun `recognizes generic RTF response and applies direct text size cap`() = withTempDirectory { directory ->
        val file = directory.resolve("generic.bin").apply { writeText("{\\rtf1\\ansi Texto genérico.}") }

        assertTrue(PlainTextBookDocument.hasRtfHeader(file))
        assertEquals(BookPayloadFormat.PLAIN_TEXT, detectBookPayloadFormat(file, "application/octet-stream"))
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, bookReaderPayloadLimit("application/rtf"))
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, bookReaderPayloadLimit("text/rtf"))
        assertTrue(PlainTextBookDocument.supports("application/x-rtf; charset=utf-8"))
    }

    @Test
    fun `omits hidden text and RTF instruction metadata`() {
        val rtf = """{\rtf1 Visible\v  hidden\v0  text {\field{\*\fldinst HYPERLINK url}{\fldrslt link}}}"""

        assertEquals("Visible text link", RtfBookTextParser.parse(rtf))
    }

    @Test
    fun `skips binary picture bytes without treating embedded braces as groups`() {
        val rtf = """{\rtf1 before {\pict\bin4 }abc} after}"""

        assertEquals("before  after", RtfBookTextParser.parse(rtf))
    }

    @Test
    fun `rejects malformed and excessively nested RTF`() {
        assertThrows(IOException::class.java) { RtfBookTextParser.parse("not an RTF document") }
        assertThrows(IOException::class.java) { RtfBookTextParser.parse("{\\rtf1{incomplete}") }
        val tooDeep = "{\\rtf1" + "{".repeat(256) + "texto" + "}".repeat(256) + "}"
        assertThrows(IOException::class.java) { RtfBookTextParser.parse(tooDeep) }
    }

    private fun withTempDirectory(block: (File) -> Unit) {
        val directory = Files.createTempDirectory("rtf-book-test").toFile()
        try {
            block(directory)
        } finally {
            directory.deleteRecursively()
        }
    }
}
