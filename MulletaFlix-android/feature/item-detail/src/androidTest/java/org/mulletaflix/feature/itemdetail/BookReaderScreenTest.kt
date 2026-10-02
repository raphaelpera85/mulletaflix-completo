package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.DeviceConfigurationOverride
import androidx.compose.ui.test.WindowSize
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.unit.DpSize
import androidx.compose.ui.unit.dp
import androidx.test.core.app.ApplicationProvider
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.readium.r2.shared.publication.Layout
import org.readium.r2.shared.publication.Publication
import org.readium.r2.shared.util.asset.AssetRetriever
import org.readium.r2.shared.util.http.DefaultHttpClient
import org.readium.r2.streamer.PublicationOpener
import org.readium.r2.streamer.parser.DefaultPublicationParser

class BookReaderScreenTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun loadingStateShowsReaderChromeWithoutRetry() {
        var backClicks = 0
        composeRule.setContent {
            MaterialTheme {
                BookReaderContent(
                    state = BookReaderUiState(isLoading = true),
                    onBack = { backClicks++ },
                    onRetry = {},
                )
            }
        }

        composeRule.onNodeWithText("Abrindo livro…").assertExists()
        composeRule.onNodeWithText("Tentar novamente").assertDoesNotExist()
        composeRule.onNodeWithContentDescription("Voltar").performClick()
        composeRule.runOnIdle { assertEquals(1, backClicks) }
    }

    @Test
    fun errorStateShowsMessageAndWiresRetry() {
        var retries = 0
        composeRule.setContent {
            MaterialTheme {
                BookReaderContent(
                    state = BookReaderUiState(errorMessage = "Falha controlada"),
                    onBack = {},
                    onRetry = { retries++ },
                )
            }
        }

        composeRule.onNodeWithText("Não foi possível abrir o livro").assertExists()
        composeRule.onNodeWithText("Falha controlada").assertExists()
        composeRule.onNodeWithText("Tentar novamente").performClick()
        composeRule.runOnIdle { assertEquals(1, retries) }
    }

    @Test
    fun loadedEpubKeepsReaderUsableAcrossPortraitAndLandscapeSizes() = runBlocking {
        val context = ApplicationProvider.getApplicationContext<Context>()
        val bookFile = File.createTempFile("mulletaflix-reader-screen-", ".epub", context.cacheDir)
        bookFile.writeBytes(minimalEpub())
        val publication = openPublication(context, bookFile)
        var readerState by mutableStateOf(BookReaderUiState(publication = publication))
        var windowSize by mutableStateOf(DpSize(411.dp, 800.dp))
        var currentLocation: String? = null
        var backClicks = 0

        try {
            composeRule.setContent {
                DeviceConfigurationOverride(
                    DeviceConfigurationOverride.WindowSize(windowSize),
                ) {
                    MaterialTheme {
                        BookReaderContent(
                            state = readerState,
                            onBack = { backClicks++ },
                            onRetry = {},
                            onLocationChanged = { currentLocation = it.href.toString() },
                        )
                    }
                }
            }

            composeRule.waitForIdle()
            composeRule.onNodeWithText("Livro de teste").assertExists()
            composeRule.waitUntil(timeoutMillis = 10_000) { currentLocation != null }
            val initialLocation = currentLocation
            composeRule.waitForEnabledButton("Próximo")
            composeRule.onNodeWithText("Próximo").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                currentLocation != null && currentLocation != initialLocation
            }
            val advancedLocation = currentLocation
            composeRule.waitForEnabledButton("Anterior")
            composeRule.onNodeWithText("Anterior").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                currentLocation == initialLocation && currentLocation != advancedLocation
            }

            composeRule.runOnIdle {
                windowSize = DpSize(800.dp, 411.dp)
            }
            composeRule.waitForIdle()
            composeRule.onNodeWithText("Livro de teste").assertExists()
            composeRule.onNodeWithText("Anterior").assertExists()
            composeRule.onNodeWithText("Próximo").assertExists()
            composeRule.onNodeWithContentDescription("Voltar").performClick()
            composeRule.runOnIdle { assertEquals(1, backClicks) }
        } finally {
            composeRule.runOnIdle {
                readerState = BookReaderUiState(errorMessage = "encerrando teste")
            }
            composeRule.waitForIdle()
            publication.close()
            bookFile.delete()
        }
    }

    @Test
    fun fixedLayoutEpubCreatesUsableReaderControls() = runBlocking {
        val context = ApplicationProvider.getApplicationContext<Context>()
        val bookFile = File.createTempFile("mulletaflix-reader-fixed-", ".epub", context.cacheDir)
        bookFile.writeBytes(minimalEpub(fixedLayout = true))
        val publication = openPublication(context, bookFile)
        var readerState by mutableStateOf(BookReaderUiState(publication = publication))
        var currentLocation: String? = null

        try {
            assertEquals(Layout.FIXED, publication.metadata.layout)
            composeRule.setContent {
                MaterialTheme {
                    BookReaderContent(
                        state = readerState,
                        onBack = {},
                        onRetry = {},
                        onLocationChanged = { currentLocation = it.href.toString() },
                    )
                }
            }

            composeRule.waitForIdle()
            composeRule.onNodeWithText("Livro de teste").assertExists()
            composeRule.waitUntil(timeoutMillis = 10_000) { currentLocation != null }
            val initialLocation = currentLocation
            composeRule.waitForEnabledButton("Próximo")
            composeRule.onNodeWithText("Próximo").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                currentLocation != null && currentLocation != initialLocation
            }
            val advancedLocation = currentLocation
            composeRule.waitForEnabledButton("Anterior")
            composeRule.onNodeWithText("Anterior").performClick()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                currentLocation == initialLocation && currentLocation != advancedLocation
            }
            Unit
        } finally {
            composeRule.runOnIdle {
                readerState = BookReaderUiState(errorMessage = "encerrando teste")
            }
            composeRule.waitForIdle()
            publication.close()
            bookFile.delete()
        }
    }

    private suspend fun openPublication(context: Context, file: File): Publication {
        val httpClient = DefaultHttpClient()
        val assetRetriever = AssetRetriever(context.contentResolver, httpClient)
        val parser = DefaultPublicationParser(
            context = context,
            httpClient = httpClient,
            assetRetriever = assetRetriever,
            pdfFactory = null,
        )
        val asset = assetRetriever.retrieve(file).getOrNull()
            ?: error("EPUB local de teste não foi reconhecido")
        return PublicationOpener(parser)
            .open(asset = asset, allowUserInteraction = false)
            .getOrNull()
            ?: run {
                asset.close()
                error("EPUB local de teste não pôde ser aberto")
            }
    }

    private fun minimalEpub(fixedLayout: Boolean = false): ByteArray {
        val output = ByteArrayOutputStream()
        val layoutMetadata = if (fixedLayout) {
            "<meta property=\"rendition:layout\">pre-paginated</meta>"
        } else {
            ""
        }
        val viewportMetadata = if (fixedLayout) {
            "<meta name=\"viewport\" content=\"width=800,height=600\"/>"
        } else {
            ""
        }
        ZipOutputStream(output).use { zip ->
            val mimetype = "application/epub+zip".toByteArray()
            val crc = CRC32().apply { update(mimetype) }
            zip.putNextEntry(ZipEntry("mimetype").apply {
                method = ZipEntry.STORED
                size = mimetype.size.toLong()
                compressedSize = mimetype.size.toLong()
                this.crc = crc.value
            })
            zip.write(mimetype)
            zip.closeEntry()

            zip.writeEntry(
                "META-INF/container.xml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                      <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/content.opf",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <package version="3.0" unique-identifier="pub-id" xmlns="http://www.idpf.org/2007/opf">
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:identifier id="pub-id">urn:uuid:mulletaflix-reader-screen-test</dc:identifier>
                        <dc:title>Livro de teste</dc:title>
                        <dc:language>pt-BR</dc:language>
                        <meta property="dcterms:modified">2026-10-02T00:00:00Z</meta>
                        $layoutMetadata
                      </metadata>
                      <manifest>
                        <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                        <item id="chapter1" href="chapter1.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter2" href="chapter2.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter3" href="chapter3.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter4" href="chapter4.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="chapter1"/><itemref idref="chapter2"/><itemref idref="chapter3"/><itemref idref="chapter4"/></spine>
                    </package>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/nav.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
                      <head><title>Sumário</title></head>
                      <body><nav epub:type="toc"><ol>
                        <li><a href="chapter1.xhtml">Capítulo 1</a></li>
                        <li><a href="chapter2.xhtml">Capítulo 2</a></li>
                        <li><a href="chapter3.xhtml">Capítulo 3</a></li>
                        <li><a href="chapter4.xhtml">Capítulo 4</a></li>
                      </ol></nav></body>
                    </html>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/chapter1.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Capítulo 1</title>$viewportMetadata</head>
                    <body><h1>Capítulo 1</h1><p>Primeira página do teste.</p></body></html>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/chapter2.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Capítulo 2</title>$viewportMetadata</head>
                    <body><h1>Capítulo 2</h1><p>Segunda página do teste.</p></body></html>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/chapter3.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Capítulo 3</title>$viewportMetadata</head>
                    <body><h1>Capítulo 3</h1><p>Terceira página do teste.</p></body></html>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/chapter4.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Capítulo 4</title>$viewportMetadata</head>
                    <body><h1>Capítulo 4</h1><p>Quarta página do teste.</p></body></html>
                """.trimIndent(),
            )
        }
        return output.toByteArray()
    }

    private fun androidx.compose.ui.test.junit4.ComposeTestRule.waitForEnabledButton(text: String) {
        waitUntil(timeoutMillis = 10_000) {
            runCatching {
                onNodeWithText(text).assertIsEnabled()
                true
            }.getOrDefault(false)
        }
    }

    private fun ZipOutputStream.writeEntry(name: String, contents: String) {
        putNextEntry(ZipEntry(name))
        write(contents.toByteArray())
        closeEntry()
    }
}
