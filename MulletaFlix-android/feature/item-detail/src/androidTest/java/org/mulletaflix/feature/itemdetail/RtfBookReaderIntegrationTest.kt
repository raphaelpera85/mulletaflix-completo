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
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class RtfBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun formattedRtfIsRenderedAsReadableText() {
        val directory = Files.createTempDirectory("rtf-reader-integration").toFile()
        try {
            val file = directory.resolve("book.rtf").apply {
                writeText("""{\rtf1\ansi Livro \b sem formatação\b0.\par Leitura direta.}""")
            }
            val document = PlainTextBookDocument.open(file, "application/rtf")

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
                .assertTextContains("Livro sem formatação.", substring = true)
                .assertTextContains("Leitura direta.", substring = true)
        } finally {
            directory.deleteRecursively()
        }
    }
}
