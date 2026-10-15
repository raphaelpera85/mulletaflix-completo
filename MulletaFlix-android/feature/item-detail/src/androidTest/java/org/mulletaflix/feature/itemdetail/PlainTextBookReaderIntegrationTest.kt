package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import android.graphics.Color
import androidx.compose.material3.MaterialTheme
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollToNode
import androidx.compose.ui.test.performScrollToIndex
import androidx.compose.ui.test.hasTestTag
import androidx.compose.ui.graphics.toPixelMap
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import android.util.Base64
import java.nio.charset.StandardCharsets
import java.io.ByteArrayOutputStream
import java.nio.file.Files
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.readium.r2.shared.publication.Locator

@RunWith(AndroidJUnit4::class)
class PlainTextBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun kf8EmbeddedImageIsResolvedAndRenderedInlineInSingleChunk() {
        val directory = Files.createTempDirectory("kf8-inline-image-integration").toFile()
        try {
            val bitmap = Bitmap.createBitmap(24, 16, Bitmap.Config.ARGB_8888)
            val imageBytes = try {
                bitmap.eraseColor(Color.MAGENTA)
                ByteArrayOutputStream().use { output ->
                    assertTrue(bitmap.compress(Bitmap.CompressFormat.PNG, 100, output))
                    output.toByteArray()
                }
            } finally {
                bitmap.recycle()
            }
            val html = Kf8ImageResources.inlineReferences(
                html = "<p>Texto antes</p><img src='kindle:embed:1?mime=image/png' alt='Diagrama KF8'><p>Texto depois</p>",
                firstImageRecord = 1,
                recordCount = 2,
                readRecord = { record ->
                    assertEquals(1, record)
                    imageBytes
                },
            )
            val file = directory.resolve("book.html").apply { writeText(html) }
            val document = PlainTextBookDocument.open(file, "text/html")
            val inlineImage = document.inlineImagesByChunk.values.flatten().single()
            assertEquals("Diagrama KF8", inlineImage.description)
            assertTrue(inlineImage.bytes.contentEquals(imageBytes))
            val currentChunk = mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = currentChunk.intValue,
                        fontSizePercent = 100,
                        onChunkSelected = { currentChunk.intValue = it },
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            val imageTag = "$PLAIN_TEXT_BOOK_INLINE_IMAGE_TEST_TAG-0-0"
            composeRule.waitUntil(timeoutMillis = 10_000) {
                runCatching { composeRule.onNodeWithTag(imageTag).assertIsDisplayed() }.isSuccess
            }
            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                .assertTextContains("Texto antes", substring = true)
                .assertTextContains("Texto depois", substring = true)
            composeRule.onNodeWithContentDescription("Diagrama KF8").assertExists()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                runCatching {
                    val pixels = composeRule.onNodeWithTag(imageTag).captureToImage().toPixelMap()
                    assertEquals(Color.MAGENTA, pixels[pixels.width / 2, pixels.height / 2].toArgb())
                }.isSuccess
            }
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun hybridMobiImageFromExtractorRendersInFocusedComposeExcerpt() {
        val context = InstrumentationRegistry.getInstrumentation().context
        val directory = Files.createTempDirectory(context.cacheDir.toPath(), "hybrid-mobi-compose").toFile()
        try {
            val kf8Bytes = context.assets.open("calibre-user-manual.azw3").use { it.readBytes() }
            val bitmap = Bitmap.createBitmap(24, 16, Bitmap.Config.ARGB_8888)
            val imageBytes = try {
                bitmap.eraseColor(Color.MAGENTA)
                ByteArrayOutputStream().use { output ->
                    assertTrue(bitmap.compress(Bitmap.CompressFormat.PNG, 100, output))
                    output.toByteArray()
                }
            } finally {
                bitmap.recycle()
            }
            val file = directory.resolve("hybrid-book.mobi").apply {
                writeBytes(hybridMobiFixture(kf8Bytes, imageBytes))
            }
            val document = PlainTextBookDocument.open(file, "application/octet-stream")
            val targetImage = document.inlineImagesByChunk.entries
                .flatMap { (chunkIndex, images) ->
                    images.mapIndexed { imageIndex, image -> Triple(chunkIndex, imageIndex, image) }
                }
                .firstOrNull { (_, _, image) -> image.bytes.contentEquals(imageBytes) }
            assertNotNull("O extrator deve resolver a imagem híbrida injetada.", targetImage)
            val (chunkIndex, _, extractedImage) = requireNotNull(targetImage)
            val sourceChunk = document.chunks[chunkIndex]
            assertTrue(sourceChunk.isNotBlank())
            val excerptStart = (extractedImage.textOffset - 32).coerceAtLeast(0)
            val excerptEnd = (extractedImage.textOffset + 33).coerceAtMost(sourceChunk.length)
            val textBefore = sourceChunk.substring(excerptStart, extractedImage.textOffset).replace("\uFFFC", "")
            val textAfter = sourceChunk.substring(
                (extractedImage.textOffset + 1).coerceAtMost(sourceChunk.length),
                excerptEnd,
            ).replace("\uFFFC", "")
            val imageDataUri = "data:${extractedImage.mimeType};base64," +
                Base64.encodeToString(extractedImage.bytes, Base64.NO_WRAP)
            val excerptHtml = "<p>${textBefore.toHtmlText()}</p>" +
                "<img src=\"$imageDataUri\" alt=\"${extractedImage.description.toHtmlText()}\"/>" +
                "<p>${textAfter.toHtmlText()}</p>"
            val excerptFile = directory.resolve("focused-image.html").apply { writeText(excerptHtml) }
            val focusedDocument = PlainTextBookDocument.open(excerptFile, "text/html")
            val focusedImage = focusedDocument.inlineImagesByChunk.values.flatten().single()
            assertTrue(focusedImage.bytes.contentEquals(extractedImage.bytes))
            val accessibleDescription = focusedImage.description.ifBlank { "Imagem incorporada no livro" }
            val currentChunk = mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = focusedDocument,
                        currentChunk = currentChunk.intValue,
                        fontSizePercent = 100,
                        onChunkSelected = { currentChunk.intValue = it },
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            val imageTag = "$PLAIN_TEXT_BOOK_INLINE_IMAGE_TEST_TAG-0-0"
            composeRule.waitUntil(timeoutMillis = 10_000) {
                runCatching { composeRule.onNodeWithTag(imageTag).assertIsDisplayed() }.isSuccess
            }
            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0").assertIsDisplayed()
            composeRule.onNodeWithContentDescription(accessibleDescription).assertExists()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                runCatching {
                    val pixels = composeRule.onNodeWithTag(imageTag).captureToImage().toPixelMap()
                    assertEquals(Color.MAGENTA, pixels[pixels.width / 2, pixels.height / 2].toArgb())
                }.isSuccess
            }
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun hybridMobiImageRendersAtOriginalPositionInFullDocumentAfterScroll() {
        val context = InstrumentationRegistry.getInstrumentation().context
        val directory = Files.createTempDirectory(context.cacheDir.toPath(), "hybrid-mobi-full-compose").toFile()
        try {
            val kf8Bytes = context.assets.open("calibre-user-manual.azw3").use { it.readBytes() }
            val bitmap = Bitmap.createBitmap(24, 16, Bitmap.Config.ARGB_8888)
            val imageBytes = try {
                bitmap.eraseColor(Color.MAGENTA)
                ByteArrayOutputStream().use { output ->
                    assertTrue(bitmap.compress(Bitmap.CompressFormat.PNG, 100, output))
                    output.toByteArray()
                }
            } finally {
                bitmap.recycle()
            }
            val file = directory.resolve("hybrid-book.mobi").apply {
                writeBytes(hybridMobiFixture(kf8Bytes, imageBytes))
            }
            val document = PlainTextBookDocument.open(file, "application/octet-stream")
            val target = document.inlineImagesByChunk.entries
                .flatMap { (chunkIndex, images) -> images.mapIndexed { imageIndex, image -> Triple(chunkIndex, imageIndex, image) } }
                .firstOrNull { (_, _, image) -> image.bytes.contentEquals(imageBytes) }
            assertNotNull("O extrator deve resolver a imagem híbrida injetada.", target)
            val (chunkIndex, imageIndex, extractedImage) = requireNotNull(target)
            val imageTag = "$PLAIN_TEXT_BOOK_INLINE_IMAGE_TEST_TAG-$chunkIndex-$imageIndex"
            val currentChunk = mutableIntStateOf(chunkIndex)

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = currentChunk.intValue,
                        fontSizePercent = 100,
                        onChunkSelected = { currentChunk.intValue = it },
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            val reader = composeRule.onNodeWithTag(PLAIN_TEXT_BOOK_READER_TEST_TAG)
            reader.performScrollToNode(hasTestTag(imageTag))
            composeRule.waitUntil(timeoutMillis = 10_000) {
                runCatching { composeRule.onNodeWithTag(imageTag).assertIsDisplayed() }.isSuccess
            }
            composeRule.onNodeWithContentDescription(extractedImage.description.ifBlank { "Imagem incorporada no livro" })
                .assertExists()
            val pixels = composeRule.onNodeWithTag(imageTag).captureToImage().toPixelMap()
            assertEquals(Color.MAGENTA, pixels[pixels.width / 2, pixels.height / 2].toArgb())
        } finally {
            directory.deleteRecursively()
        }
    }

    private fun hybridMobiFixture(kf8Bytes: ByteArray, legacyImage: ByteArray): ByteArray {
        val recordCount = readUnsignedShort(kf8Bytes, 76)
        val recordOffsets = List(recordCount) { index -> readUnsignedInt(kf8Bytes, 78 + index * 8) }
        val kf8Records = recordOffsets.mapIndexed { index, offset ->
            val end = recordOffsets.getOrElse(index + 1) { kf8Bytes.size }
            kf8Bytes.copyOfRange(offset, end)
        }
        val shiftedKf8Header = kf8Records.first().copyOf().apply {
            listOf(192, 248, 252).forEach { offset -> writeInt(this, offset, readUnsignedInt(this, offset) + 4) }
        }
        val legacyHeader = ByteArray(272).apply {
            writeShort(this, 0, 1)
            writeInt(this, 4, LEGACY_TEXT.length)
            writeShort(this, 8, 1)
            writeShort(this, 10, 4_096)
            writeAscii(this, 16, "MOBI")
            writeInt(this, 20, 232)
            writeInt(this, 24, 2)
            writeInt(this, 28, 65_001)
            writeInt(this, 104, 6)
            writeInt(this, 108, -1)
            writeInt(this, 128, 0x40)
            writeAscii(this, 248, "EXTH")
            writeInt(this, 252, 24)
            writeInt(this, 256, 1)
            writeInt(this, 260, 121)
            writeInt(this, 264, 12)
            writeInt(this, 268, 4)
        }
        val records = listOf(
            legacyHeader,
            LEGACY_TEXT.toByteArray(StandardCharsets.US_ASCII),
            legacyImage,
            "BOUNDARY".toByteArray(StandardCharsets.US_ASCII),
            shiftedKf8Header,
        ) + kf8Records.drop(1)
        val recordTableEnd = 78 + records.size * 8
        val offsets = records.indices.map { index -> recordTableEnd + records.take(index).sumOf { it.size } }
        val result = ByteArray(recordTableEnd + records.sumOf { it.size })
        writeAscii(result, 0, "Hybrid AZW3 Compose fixture")
        writeAscii(result, 60, "BOOKMOBI")
        writeShort(result, 76, records.size)
        offsets.forEachIndexed { index, offset -> writeInt(result, 78 + index * 8, offset) }
        records.forEachIndexed { index, record -> record.copyInto(result, offsets[index]) }
        return result
    }

    private fun readUnsignedShort(bytes: ByteArray, offset: Int): Int =
        ((bytes[offset].toInt() and 0xFF) shl 8) or (bytes[offset + 1].toInt() and 0xFF)

    private fun readUnsignedInt(bytes: ByteArray, offset: Int): Int =
        ((bytes[offset].toInt() and 0xFF) shl 24) or
            ((bytes[offset + 1].toInt() and 0xFF) shl 16) or
            ((bytes[offset + 2].toInt() and 0xFF) shl 8) or
            (bytes[offset + 3].toInt() and 0xFF)

    private fun writeShort(bytes: ByteArray, offset: Int, value: Int) {
        bytes[offset] = (value ushr 8).toByte()
        bytes[offset + 1] = value.toByte()
    }

    private fun writeInt(bytes: ByteArray, offset: Int, value: Int) {
        bytes[offset] = (value ushr 24).toByte()
        bytes[offset + 1] = (value ushr 16).toByte()
        bytes[offset + 2] = (value ushr 8).toByte()
        bytes[offset + 3] = value.toByte()
    }

    private fun writeAscii(bytes: ByteArray, offset: Int, value: String) {
        value.toByteArray(StandardCharsets.US_ASCII).copyInto(bytes, offset)
    }

    private fun String.toHtmlText(): String = replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace("\"", "&quot;")
        .replace("'", "&#39;")

    private companion object {
        const val LEGACY_TEXT = "LEGACY MOBI 7 CONTENT"
    }

    @Test
    fun textIsReadableAndScrollingUpdatesTheRestorableChunk() {
        val directory = Files.createTempDirectory("txt-reader-integration").toFile()
        try {
            val file = directory.resolve("book.txt").apply {
                writeText((0..900).joinToString("\n") { "Capítulo $it — texto de leitura em português." })
            }
            val document = PlainTextBookDocument.open(file)
            val currentChunk = mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = currentChunk.intValue,
                        fontSizePercent = 100,
                        onChunkSelected = { currentChunk.intValue = it },
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            composeRule.onNodeWithTag(PLAIN_TEXT_BOOK_READER_TEST_TAG).assertIsDisplayed()
            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                .assertExists()
                .assertTextContains("Capítulo 0 — texto de leitura em português.", substring = true)
            composeRule.onNodeWithTag(PLAIN_TEXT_BOOK_READER_TEST_TAG).performScrollToIndex(1)
            composeRule.waitUntil(timeoutMillis = 5_000) { currentChunk.intValue == 1 }
            composeRule.runOnIdle { assertEquals(1, currentChunk.intValue) }
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun htmlBookRendersAsScrollableTextWithoutExecutingMarkup() {
        val directory = Files.createTempDirectory("html-reader-integration").toFile()
        try {
            val file = directory.resolve("book.html").apply {
                writeText(
                    """<html><head><script>window.bad=true</script></head><body>""" +
                        """<p hidden>Hidden attribute</p><span style="display:none">Hidden style</span>""" +
                        """<h1>Livro HTML</h1><p>Conteúdo seguro &amp; acessível.</p></body></html>""",
                )
            }
            val document = PlainTextBookDocument.open(file, "text/html; charset=utf-8")
            val currentChunk = mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = currentChunk.intValue,
                        fontSizePercent = 100,
                        onChunkSelected = { currentChunk.intValue = it },
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                .assertIsDisplayed()
                .assertTextContains("Livro HTML", substring = true)
                .assertTextContains("Conteúdo seguro & acessível.", substring = true)
            composeRule.onNodeWithText("window.bad=true").assertDoesNotExist()
            composeRule.onNodeWithText("Hidden attribute").assertDoesNotExist()
            composeRule.onNodeWithText("Hidden style").assertDoesNotExist()
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun markdownBookRendersReadableContentInCompose() {
        val directory = Files.createTempDirectory("markdown-reader-integration").toFile()
        try {
            val file = directory.resolve("book.md").apply {
                writeText(
                    """# Livro Markdown

                        Texto **legível** com `código` e [um link](https://example.test).

                        - [x] Capítulo um

                        ```text
                        Conteúdo do bloco
                        ```
                    """.trimIndent(),
                )
            }
            val document = PlainTextBookDocument.open(file, "text/markdown; charset=utf-8")

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = 0,
                        fontSizePercent = 100,
                        onChunkSelected = {},
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                .assertIsDisplayed()
                .assertTextContains("Livro Markdown", substring = true)
                .assertTextContains("Texto legível com código e um link.", substring = true)
                .assertTextContains("☑ Capítulo um", substring = true)
                .assertTextContains("Conteúdo do bloco", substring = true)
            composeRule.onNodeWithText("**legível**").assertDoesNotExist()
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun markdownTableOfContentsNavigatesToSecondChunkAndResolvesItsLocator() {
        val directory = Files.createTempDirectory("markdown-toc-integration").toFile()
        try {
            val file = directory.resolve("chapters.md").apply {
                writeText(buildString {
                    appendLine("# Primeiro capítulo")
                    appendLine()
                    repeat(30) { appendLine("Texto introdutório suficiente para atravessar um bloco de leitura.") }
                    appendLine()
                    appendLine("## Segundo capítulo")
                    appendLine()
                    append("Corpo exclusivo do segundo capítulo.")
                })
            }
            val document = PlainTextBookDocument.open(file, "text/markdown; charset=utf-8")
            assertEquals(listOf("Primeiro capítulo", "Segundo capítulo"), document.chapters.map { it.title })
            val selectedChunk = mutableIntStateOf(0)
            var savedLocator: Locator? = null
            val entries = document.chapters.map { chapter ->
                BookReaderContentsEntry(
                    title = chapter.title,
                    depth = chapter.depth,
                    chapterChunkIndex = chapter.chunkIndex,
                )
            }
            val targetChapter = document.chapters[1]

            assertEquals(0, document.chapters[0].chunkIndex)
            assertEquals(1, targetChapter.chunkIndex)

            composeRule.setContent {
                MaterialTheme {
                    Column(Modifier.fillMaxSize()) {
                        BookReaderContentsActions(entries = entries, enabled = true) { entry ->
                            val chunk = requireNotNull(entry.chapterChunkIndex)
                            selectedChunk.intValue = chunk
                            savedLocator = document.locatorForChunk(chunk)
                        }
                        PlainTextBookReaderContent(
                            document = document,
                            currentChunk = selectedChunk.intValue,
                            fontSizePercent = 100,
                            onChunkSelected = { selectedChunk.intValue = it },
                            modifier = Modifier.weight(1f),
                        )
                    }
                }
            }

            composeRule.onNodeWithContentDescription("Abrir sumário").performClick()
            composeRule.onNodeWithText("Segundo capítulo").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                selectedChunk.intValue == targetChapter.chunkIndex
            }
            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-${targetChapter.chunkIndex}")
                .assertIsDisplayed()
                .assertTextContains("Segundo capítulo", substring = true)
                .assertTextContains("Corpo exclusivo do segundo capítulo.", substring = true)
            assertEquals(targetChapter.chunkIndex, document.chunkIndexFromLocator(savedLocator))
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun fictionBookXmlRendersOnlyReadableBodyContent() {
        val directory = Files.createTempDirectory("fb2-reader-integration").toFile()
        try {
            val file = directory.resolve("book.fb2").apply {
                writeText(
                    """<?xml version="1.0" encoding="UTF-8"?>
                        <FictionBook xmlns="http://www.gribuser.ru/xml/fictionbook/2.0">
                          <description><title-info><book-title>Metadata</book-title></title-info></description>
                          <body><title><p>Capítulo FB2</p></title><section>
                            <p>Texto FictionBook &mdash; seguro &amp; legível.</p>
                          </section></body>
                        </FictionBook>""".trimIndent(),
                )
            }
            val document = PlainTextBookDocument.open(file, "application/xml; charset=utf-8")

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = 0,
                        fontSizePercent = 100,
                        onChunkSelected = {},
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                .assertIsDisplayed()
                .assertTextContains("Capítulo FB2", substring = true)
                .assertTextContains("Texto FictionBook — seguro & legível.", substring = true)
            composeRule.onNodeWithText("Metadata").assertDoesNotExist()
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun fictionBookTableOfContentsJumpsToChapterAndRestoresItsLocator() {
        val directory = Files.createTempDirectory("fb2-toc-integration").toFile()
        try {
            val file = directory.resolve("chapters.fb2").apply {
                writeText(
                    """<FictionBook><body><section><title><p>Parte I</p></title>
                        <section><title><p>Capítulo 1</p></title><p>${"texto ".repeat(240)}</p></section>
                        <section><title><p>Capítulo 2 &amp; fim</p></title><p>Última parte legível.</p></section>
                    </section></body></FictionBook>""".trimIndent(),
                )
            }
            val document = PlainTextBookDocument.open(file, "application/x-fictionbook+xml")
            val selectedChunk = mutableIntStateOf(0)
            var savedLocator: Locator? = null
            val entries = document.chapters.map { chapter ->
                BookReaderContentsEntry(
                    title = chapter.title,
                    depth = chapter.depth,
                    chapterChunkIndex = chapter.chunkIndex,
                )
            }

            composeRule.setContent {
                MaterialTheme {
                    Column(Modifier.fillMaxSize()) {
                        BookReaderContentsActions(entries = entries, enabled = true) { entry ->
                            val chunk = requireNotNull(entry.chapterChunkIndex)
                            selectedChunk.intValue = chunk
                            savedLocator = document.locatorForChunk(chunk)
                        }
                        PlainTextBookReaderContent(
                            document = document,
                            currentChunk = selectedChunk.intValue,
                            fontSizePercent = 100,
                            onChunkSelected = { selectedChunk.intValue = it },
                            modifier = Modifier.weight(1f),
                        )
                    }
                }
            }

            composeRule.onNodeWithContentDescription("Abrir sumário").performClick()
            composeRule.onNodeWithText("Capítulo 2 & fim").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                selectedChunk.intValue == document.chapters.last().chunkIndex
            }
            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-${document.chapters.last().chunkIndex}")
                .assertIsDisplayed()
                .assertTextContains("Capítulo 2 & fim", substring = true)
            assertEquals(
                document.chapters.last().chunkIndex,
                document.chunkIndexFromLocator(savedLocator),
            )
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun genericUtf16FictionBookWithoutBomRendersReadableText() {
        val directory = Files.createTempDirectory("fb2-utf16-reader-integration").toFile()
        try {
            val xml = """<?xml version="1.0" encoding="UTF-16"?>
                <FictionBook><body><section><p>Livro em UTF-16: leitura na TV e no celular.</p></section></body></FictionBook>""".trimIndent()
            val file = directory.resolve("book.fb2").apply {
                writeBytes(xml.toByteArray(StandardCharsets.UTF_16BE))
            }
            val document = PlainTextBookDocument.open(file, "application/octet-stream")

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = 0,
                        fontSizePercent = 100,
                        onChunkSelected = {},
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                .assertIsDisplayed()
                .assertTextContains("Livro em UTF-16: leitura na TV e no celular.", substring = true)
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun zippedFictionBookRendersReadableTextFromGenericZip() {
        val directory = Files.createTempDirectory("fb2-zip-reader-integration").toFile()
        try {
            val file = directory.resolve("book.fb2.zip")
            ZipOutputStream(file.outputStream()).use { zip ->
                zip.putNextEntry(ZipEntry("book.fb2"))
                zip.write(
                    """<?xml version="1.0" encoding="UTF-8"?><FictionBook><body><section><p>Livro FictionBook ZIP legível.</p></section></body></FictionBook>"""
                        .toByteArray(StandardCharsets.UTF_8),
                )
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("cover.jpg"))
                zip.write(byteArrayOf(1, 2, 3))
                zip.closeEntry()
            }
            val document = PlainTextBookDocument.open(file, "application/octet-stream")

            composeRule.setContent {
                MaterialTheme {
                    PlainTextBookReaderContent(
                        document = document,
                        currentChunk = 0,
                        fontSizePercent = 100,
                        onChunkSelected = {},
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-0")
                .assertIsDisplayed()
                .assertTextContains("Livro FictionBook ZIP legível.", substring = true)
        } finally {
            directory.deleteRecursively()
        }
    }
}
