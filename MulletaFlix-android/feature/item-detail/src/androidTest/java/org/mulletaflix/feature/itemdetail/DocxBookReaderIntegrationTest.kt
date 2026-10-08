package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.nio.file.Files
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
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
}
