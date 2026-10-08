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
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class OdtBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun extractedOdtTextIsRenderedForReading() {
        val directory = Files.createTempDirectory("odt-reader-integration").toFile()
        try {
            val file = directory.resolve("book.odt")
            ZipOutputStream(file.outputStream()).use { zip ->
                val mimeBytes = OdtBookTextExtractor.CONTENT_TYPE.toByteArray(Charsets.US_ASCII)
                zip.putNextEntry(
                    ZipEntry("mimetype").apply {
                        method = ZipEntry.STORED
                        size = mimeBytes.size.toLong()
                        compressedSize = mimeBytes.size.toLong()
                        crc = CRC32().apply { update(mimeBytes) }.value
                    },
                )
                zip.write(mimeBytes)
                zip.closeEntry()
                zip.putNextEntry(ZipEntry("content.xml"))
                zip.write(
                    """<office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"><office:body><office:text><text:h>Leitura ODT direta.</text:h><text:p>Parágrafo de teste.</text:p></office:text></office:body></office:document-content>"""
                        .toByteArray(Charsets.UTF_8),
                )
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
                .assertTextContains("Leitura ODT direta.", substring = true)
                .assertTextContains("Parágrafo de teste.", substring = true)
        } finally {
            directory.deleteRecursively()
        }
    }
}
