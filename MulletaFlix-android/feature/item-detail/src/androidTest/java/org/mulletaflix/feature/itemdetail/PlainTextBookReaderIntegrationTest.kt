package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performScrollToIndex
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class PlainTextBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

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
