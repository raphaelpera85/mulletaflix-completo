package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.nio.file.Files
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DocxBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun extractedDocxTextIsRenderedForReading() {
        val directory = Files.createTempDirectory("docx-reader-integration").toFile()
        try {
            val file = directory.resolve("book.docx")
            ZipOutputStream(file.outputStream()).use { zip ->
                zip.putNextEntry(ZipEntry("[Content_Types].xml"))
                zip.write("<Types/>".toByteArray())
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("word/document.xml"))
                zip.write("""<w:document xmlns:w="urn:word"><w:body><w:p><w:r><w:t>Leitura DOCX direta.</w:t></w:r></w:p><w:p><w:r><w:t>Segundo parágrafo.</w:t></w:r></w:p></w:body></w:document>""".toByteArray())
                zip.closeEntry()
            }
            val document = PlainTextBookDocument.open(file, DocxBookTextExtractor.CONTENT_TYPE)

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
                .assertTextContains("Leitura DOCX direta.", substring = true)
                .assertTextContains("Segundo parágrafo.", substring = true)
        } finally {
            directory.deleteRecursively()
        }
    }

    @Test
    fun docxTableOfContentsJumpsToWordHeadingAndRestoresLocator() {
        val directory = Files.createTempDirectory("docx-toc-integration").toFile()
        try {
            val file = directory.resolve("chapters.docx")
            val body = """<w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Parte I</w:t></w:r></w:p>
                <w:p><w:r><w:t>${"Texto inicial. ".repeat(300)}</w:t></w:r></w:p>
                <w:p><w:pPr><w:outlineLvl w:val="1"/></w:pPr><w:r><w:t>Capítulo 1</w:t></w:r></w:p>
                <w:p><w:pPr><w:pStyle w:val="CustomChapter"/></w:pPr><w:r><w:t>Capítulo herdado personalizado</w:t></w:r></w:p>
                <w:p><w:r><w:t>Conteúdo do capítulo.</w:t></w:r></w:p>""".trimIndent()
            val styles = """<w:styles xmlns:w="urn:word">
                <w:style w:type="paragraph" w:styleId="CustomChapter"><w:basedOn w:val="LocalIntermediate"/></w:style>
                <w:style w:type="paragraph" w:styleId="LocalIntermediate"><w:basedOn w:val="Heading1"/></w:style>
                </w:styles>"""
            ZipOutputStream(file.outputStream()).use { zip ->
                zip.putNextEntry(ZipEntry("[Content_Types].xml"))
                zip.write("<Types/>".toByteArray())
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("word/document.xml"))
                zip.write("<w:document xmlns:w=\"urn:word\"><w:body>$body</w:body></w:document>".toByteArray())
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("word/styles.xml"))
                zip.write(styles.toByteArray())
                zip.closeEntry()
            }
            val document = PlainTextBookDocument.open(file, DocxBookTextExtractor.CONTENT_TYPE)
            val selectedChunk = androidx.compose.runtime.mutableIntStateOf(0)
            var savedLocator: org.readium.r2.shared.publication.Locator? = null
            val entries = document.chapters.map { chapter ->
                BookReaderContentsEntry(chapter.title, chapter.depth, chapterChunkIndex = chapter.chunkIndex)
            }

            composeRule.setContent {
                MaterialTheme {
                    androidx.compose.foundation.layout.Column(Modifier.fillMaxSize()) {
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
            composeRule.onNodeWithText("Capítulo 1").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                selectedChunk.intValue == document.chapters[1].chunkIndex
            }
            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-${document.chapters[1].chunkIndex}")
                .assertIsDisplayed()
                .assertTextContains("Capítulo 1", substring = true)
            assertEquals(document.chapters[1].chunkIndex, document.chunkIndexFromLocator(savedLocator))

            composeRule.onNodeWithContentDescription("Abrir sumário").performClick()
            composeRule.onNodeWithText("Capítulo herdado personalizado").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                selectedChunk.intValue == document.chapters.last().chunkIndex
            }
            composeRule.onNodeWithTag("$PLAIN_TEXT_BOOK_CHUNK_TEST_TAG-${document.chapters.last().chunkIndex}")
                .assertIsDisplayed()
                .assertTextContains("Capítulo herdado personalizado", substring = true)
            assertEquals(document.chapters.last().chunkIndex, document.chunkIndexFromLocator(savedLocator))
        } finally {
            directory.deleteRecursively()
        }
    }
}
