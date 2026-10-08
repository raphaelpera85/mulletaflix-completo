package org.mulletaflix.feature.itemdetail

import java.io.File
import java.nio.charset.Charset
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class PlainTextBookDocumentTest {
    @Test
    fun `reads UTF-8 BOM UTF-16 and Windows-1252 text`() = withTempDirectory { directory ->
        val utf8 = directory.resolve("utf8.txt").apply {
            writeBytes(byteArrayOf(0xEF.toByte(), 0xBB.toByte(), 0xBF.toByte()) + "Olá, mundo!".toByteArray())
        }
        val utf16Le = directory.resolve("utf16-le.txt").apply {
            writeBytes(byteArrayOf(0xFF.toByte(), 0xFE.toByte()) + "Olá, mundo!".toByteArray(StandardCharsets.UTF_16LE))
        }
        val utf16Be = directory.resolve("utf16-be.txt").apply {
            writeBytes(byteArrayOf(0xFE.toByte(), 0xFF.toByte()) + "Olá, mundo!".toByteArray(StandardCharsets.UTF_16BE))
        }
        val windows1252 = directory.resolve("windows-1252.txt").apply {
            writeBytes("Ação: café".toByteArray(Charset.forName("windows-1252")))
        }

        listOf(utf8, utf16Le, utf16Be).forEach { file ->
            assertEquals("Olá, mundo!", PlainTextBookDocument.open(file).chunks.joinToString(""))
        }
        assertEquals("Ação: café", PlainTextBookDocument.open(windows1252).chunks.joinToString(""))
    }

    @Test
    fun `splits long text without losing characters or breaking surrogate pairs`() = withTempDirectory { directory ->
        val text = buildString {
            repeat(20_000) { append(if (it % 19 == 0) "🙂\n" else "palavra$it ") }
        }
        val file = directory.resolve("long.txt").apply { writeText(text) }

        val document = PlainTextBookDocument.open(file)

        assertTrue(document.chunkCount > 1)
        assertEquals(text, document.chunks.joinToString(""))
        assertTrue(document.chunks.none { it.isNotEmpty() && Character.isHighSurrogate(it.last()) })
    }

    @Test
    fun `reading locators restore valid chunk and reject invalid chunk`() = withTempDirectory { directory ->
        val file = directory.resolve("many-lines.txt").apply {
            writeText((0..500).joinToString("\n") { "Line $it" })
        }
        val document = PlainTextBookDocument.open(file)
        assertTrue(document.chunkCount > 1)
        val locator = document.locatorForChunk(1)

        assertEquals(1, document.chunkIndexFromLocator(locator))
        // An unknown locator must not be treated as the beginning of this text document.
        assertNull(
            document.chunkIndexFromLocator(
                org.readium.r2.shared.publication.Locator.fromJSON(
                    org.json.JSONObject().put("href", "other-book"),
                ),
            ),
        )
        assertThrows(IllegalArgumentException::class.java) { document.locatorForChunk(document.chunkCount) }
    }

    @Test
    fun `accepts direct plain text and HTML fallback`() {
        assertTrue(PlainTextBookDocument.supports("Text/Plain; charset=utf-8"))
        assertFalse(isClearlyNotSupportedBookContentType("text/plain; charset=utf-8"))
        assertTrue(PlainTextBookDocument.supports("text/html"))
        assertTrue(PlainTextBookDocument.isHtml("text/html; charset=utf-8"))
        assertFalse(isClearlyNotSupportedBookContentType("text/html; charset=utf-8"))
    }

    @Test
    fun `reads common Markdown as safe plain text and keeps the direct text size limit`() = withTempDirectory { directory ->
        val file = directory.resolve("book.md").apply {
            writeText(
                """# Guia de leitura

                    Texto **importante**, *simples*, `código` e [site](https://example.test).

                    - [x] Item concluído
                    - [ ] Item pendente
                    1. Primeiro
                    > Uma citação

                    ```kotlin
                    val answer = 42
                    ```

                    | Nome | Estado |
                    | --- | --- |
                    | Filme | pronto |
                    ![Capa](cover.jpg)
                    <script>alert(1)</script>
                """.trimIndent(),
            )
        }

        val contentType = "text/markdown; charset=utf-8"
        val rendered = PlainTextBookDocument.open(file, contentType).chunks.joinToString("")

        assertTrue(PlainTextBookDocument.supports(contentType))
        assertTrue(PlainTextBookDocument.supports("text/x-markdown"))
        assertEquals(PlainTextBookDocument.MAX_TEXT_BYTES, bookReaderPayloadLimit(contentType))
        assertFalse(isClearlyNotSupportedBookContentType(contentType))
        assertTrue(rendered.contains("Guia de leitura"))
        assertTrue(rendered.contains("Texto importante, simples, código e site."))
        assertTrue(rendered.contains("☑ Item concluído"))
        assertTrue(rendered.contains("☐ Item pendente"))
        assertTrue(rendered.contains("Uma citação"))
        assertTrue(rendered.contains("val answer = 42"))
        assertTrue(rendered.contains("Nome    |    Estado"))
        assertTrue(rendered.contains("Capa"))
        assertTrue(rendered.contains("<script>alert(1)</script>"))
        assertFalse(rendered.contains("**importante**"))
        assertFalse(rendered.contains("https://example.test"))
        assertFalse(rendered.contains("cover.jpg"))
    }

    @Test
    fun `reads DOCX paragraphs and detects generic ZIP MIME before comic classification`() = withTempDirectory { directory ->
        val file = directory.resolve("book.docx")
        writeDocx(file, """<w:document xmlns:w="urn:word"><w:body>
            <w:p><w:r><w:t>Capítulo &amp; introdução&#x2019;s</w:t></w:r></w:p>
            <w:p><w:r><w:t>Texto principal.</w:t></w:r><w:del><w:r><w:delText>Removido.</w:delText></w:r></w:del></w:p>
            <w:tbl><w:tr><w:tc><w:p><w:r><w:t>Coluna A</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Coluna B</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
            </w:body></w:document>""")

        assertTrue(PlainTextBookDocument.supports(DocxBookTextExtractor.CONTENT_TYPE))
        assertEquals(DocxBookTextExtractor.MAX_PACKAGE_BYTES, bookReaderPayloadLimit(DocxBookTextExtractor.CONTENT_TYPE))
        assertEquals(BookPayloadFormat.PLAIN_TEXT, detectBookPayloadFormat(file, "application/octet-stream"))
        val text = PlainTextBookDocument.open(file, "application/octet-stream").chunks.joinToString("")
        assertTrue(text.contains("Capítulo & introdução’s"))
        assertTrue(text.contains("Texto principal."))
        assertTrue(text.contains("Coluna A\tColuna B"))
        assertFalse(text.contains("Removido."))
    }

    @Test
    fun `rejects invalid DOCX packages and oversized document XML`() = withTempDirectory { directory ->
        val invalid = directory.resolve("invalid.docx").apply { writeText("not a ZIP") }
        assertThrows(java.io.IOException::class.java) {
            PlainTextBookDocument.open(invalid, DocxBookTextExtractor.CONTENT_TYPE)
        }

        val oversized = directory.resolve("oversized.docx")
        writeDocx(
            oversized,
            "<w:document xmlns:w=\"urn:word\"><w:body><w:p><w:r><w:t>${"x".repeat(DocxBookTextExtractor.MAX_DOCUMENT_XML_BYTES.toInt())}</w:t></w:r></w:p></w:body></w:document>",
        )
        assertThrows(java.io.IOException::class.java) {
            PlainTextBookDocument.open(oversized, DocxBookTextExtractor.CONTENT_TYPE)
        }

        val externalEntity = directory.resolve("external-entity.docx")
        writeDocx(
            externalEntity,
            """<!DOCTYPE w:document [<!ENTITY secret SYSTEM "file:///etc/passwd">]><w:document xmlns:w="urn:word"><w:body><w:p><w:r><w:t>&secret;</w:t></w:r></w:p></w:body></w:document>""",
        )
        assertThrows(java.io.IOException::class.java) {
            PlainTextBookDocument.open(externalEntity, DocxBookTextExtractor.CONTENT_TYPE)
        }
    }

    private fun writeDocx(file: File, documentXml: String) {
        ZipOutputStream(file.outputStream()).use { zip ->
            zip.putNextEntry(ZipEntry("[Content_Types].xml"))
            zip.write("<Types/>".toByteArray())
            zip.closeEntry()
            zip.putNextEntry(ZipEntry("word/document.xml"))
            zip.write(documentXml.toByteArray())
            zip.closeEntry()
            zip.putNextEntry(ZipEntry("word/media/image1.jpeg"))
            zip.write(byteArrayOf(1, 2, 3))
            zip.closeEntry()
        }
    }

    @Test
    fun `reads FictionBook body text and common named entities`() = withTempDirectory { directory ->
        val file = directory.resolve("book.fb2").apply {
            writeText(
                """<?xml version="1.0" encoding="UTF-8"?>
                    <FictionBook xmlns="http://www.gribuser.ru/xml/fictionbook/2.0">
                      <description><title-info><book-title>Metadata must stay outside body</book-title></title-info></description>
                      <body><title><p>Capítulo 1</p></title>
                        <section><p>Linha 1 &mdash; segura &amp; simples</p>
                          <poem><stanza><v>Linha 2</v></stanza></poem>
                        </section>
                        <binary id="cover">SWdub3JlIGltYWdlIGRhdGE=</binary>
                      </body>
                    </FictionBook>""".trimIndent(),
            )
        }

        assertTrue(PlainTextBookDocument.supports("application/x-fictionbook+xml"))
        val text = PlainTextBookDocument.open(file, "application/x-fictionbook+xml").chunks.joinToString("")
        assertTrue(text.contains("Capítulo 1"))
        assertTrue(text.contains("Linha 1 — segura & simples"))
        assertTrue(text.contains("Linha 2"))
        assertFalse(text.contains("Metadata must stay outside body"))
        assertFalse(text.contains("SWdub3Jl"))
    }

    @Test
    fun `reads generic FictionBook XML encoded as UTF-16 without a BOM`() = withTempDirectory { directory ->
        val xml = """<?xml version="1.0" encoding="UTF-16"?>
            <FictionBook><body><section><p>Leitura sem BOM: São Paulo &amp; ação.</p></section></body></FictionBook>""".trimIndent()
        val utf16Be = directory.resolve("book-be.fb2").apply {
            writeBytes(xml.toByteArray(StandardCharsets.UTF_16BE))
        }
        val utf16Le = directory.resolve("book-le.fb2").apply {
            writeBytes(xml.toByteArray(StandardCharsets.UTF_16LE))
        }

        listOf(utf16Be, utf16Le).forEach { file ->
            val text = PlainTextBookDocument.open(file, "application/octet-stream")
                .chunks
                .joinToString("")

            assertTrue(text.contains("Leitura sem BOM: São Paulo & ação."))
        }
    }

    @Test
    fun `rejects FictionBook DTD and external entity declarations`() = withTempDirectory { directory ->
        val file = directory.resolve("unsafe.fb2").apply {
            writeText(
                """<!DOCTYPE FictionBook SYSTEM "file:///etc/passwd">
                    <FictionBook><body><p>&secret;</p></body></FictionBook>""".trimIndent(),
            )
        }

        assertThrows(java.io.IOException::class.java) {
            PlainTextBookDocument.open(file, "application/x-fictionbook+xml")
        }
    }

    @Test
    fun `converts HTML into readable text without active or hidden document content`() = withTempDirectory { directory ->
        val file = directory.resolve("book.html").apply {
            writeText(
                """<html><head><title>Hidden title</title><style>.x{display:none}</style></head>""" +
                    """<body><h1>Capítulo 1</h1><p>Olá &amp; bem-vindo.</p>""" +
                    """<script>alert('never show')</script><embed src="video"> <p>Fim.</p></body></html>""",
            )
        }

        val text = PlainTextBookDocument.open(file, "text/html; charset=utf-8").chunks.joinToString("")

        assertTrue(text.contains("Capítulo 1"))
        assertTrue(text.contains("Olá & bem-vindo."))
        assertTrue(text.contains("Fim."))
        assertFalse(text.contains("Hidden title"))
        assertFalse(text.contains("display:none"))
        assertFalse(text.contains("alert"))
        assertFalse(text.contains("src=\"video\""))
    }

    @Test
    fun `removes hidden HTML elements including nested and inline styles`() = withTempDirectory { directory ->
        val file = directory.resolve("hidden.html").apply {
            writeText(
                "<p>Visible before</p>" +
                    "<div hidden/>Hidden attribute <div>Nested hidden text" +
                    "<script>const fakeClose = \"</div>\";</script></div> tail</div>" +
                    "<span style='color:red; display : none !important'>Hidden display</span>" +
                    "<p style=\"visibility:hidden\">Hidden visibility</p>" +
                    "<section aria-hidden='true'>Hidden aria text</section>" +
                    "<textarea><div hidden>Literal tag text</div></textarea>" +
                    "<p>Visible after</p>",
            )
        }

        val text = PlainTextBookDocument.open(file, "text/html").chunks.joinToString("")

        assertTrue(text.contains("Visible before"))
        assertTrue(text.contains("Visible after"))
        assertTrue(text.contains("Literal tag text"))
        assertFalse(text.contains("Hidden attribute"))
        assertFalse(text.contains("Nested hidden text"))
        assertFalse(text.contains("fakeClose"))
        assertFalse(text.contains("tail"))
        assertFalse(text.contains("Hidden display"))
        assertFalse(text.contains("Hidden visibility"))
        assertFalse(text.contains("Hidden aria text"))
    }

    @Test
    fun `malformed HTML with many unclosed active tags is scanned safely`() = withTempDirectory { directory ->
        val file = directory.resolve("malformed.html").apply {
            writeText("Visible start " + "<script>hidden".repeat(20_000))
        }

        val text = PlainTextBookDocument.open(file, "text/html").chunks.joinToString("")

        assertTrue(text.startsWith("Visible start"))
        assertFalse(text.contains("hidden"))
    }

    @Test
    fun `does not parse active-looking tags inside HTML comments`() = withTempDirectory { directory ->
        val file = directory.resolve("comment.html").apply {
            writeText("Before <!-- comment > <script>not an element --> <p>Visible after comment</p>")
        }

        val text = PlainTextBookDocument.open(file, "text/html").chunks.joinToString("")

        assertTrue(text.contains("Before"))
        assertTrue(text.contains("Visible after comment"))
        assertFalse(text.contains("not an element"))
    }

    @Test
    fun `honors declared HTML charset and rejects unsupported response types`() = withTempDirectory { directory ->
        val file = directory.resolve("latin1.html").apply {
            writeBytes("<p>Ação: café</p>".toByteArray(Charset.forName("windows-1252")))
        }

        assertTrue(
            PlainTextBookDocument.open(file, "text/html; charset=windows-1252")
                .chunks.joinToString("").contains("Ação: café"),
        )
        assertThrows(java.io.IOException::class.java) {
            PlainTextBookDocument.open(file, "text/xml")
        }
    }

    @Test
    fun `rejects empty and oversized direct text files`() = withTempDirectory { directory ->
        val empty = directory.resolve("empty.txt").apply { writeBytes(byteArrayOf()) }
        assertThrows(java.io.IOException::class.java) { PlainTextBookDocument.open(empty) }

        val oversized = directory.resolve("oversized.txt").apply {
            java.io.RandomAccessFile(this, "rw").use { file ->
                file.setLength(PlainTextBookDocument.MAX_TEXT_BYTES + 1L)
            }
        }
        assertThrows(java.io.IOException::class.java) { PlainTextBookDocument.open(oversized) }

        val oversizedHtml = directory.resolve("oversized.html").apply {
            java.io.RandomAccessFile(this, "rw").use { file ->
                file.setLength(PlainTextBookDocument.MAX_HTML_BYTES + 1L)
            }
        }
        assertThrows(java.io.IOException::class.java) {
            PlainTextBookDocument.open(oversizedHtml, "text/html")
        }
    }

    private inline fun withTempDirectory(block: (File) -> Unit) {
        val directory = Files.createTempDirectory("plain-text-book-test").toFile()
        try {
            block(directory)
        } finally {
            directory.deleteRecursively()
        }
    }
}
