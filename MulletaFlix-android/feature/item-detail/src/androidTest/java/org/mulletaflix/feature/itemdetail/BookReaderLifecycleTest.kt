@file:OptIn(org.readium.r2.shared.ExperimentalReadiumApi::class)

package org.mulletaflix.feature.itemdetail

import android.content.Context
import android.content.pm.ActivityInfo
import android.content.res.Configuration
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.test.core.app.ApplicationProvider
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlin.math.abs
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.readium.r2.shared.publication.Locator
import org.readium.r2.shared.publication.Publication
import org.readium.r2.shared.util.asset.AssetRetriever
import org.readium.r2.shared.util.http.DefaultHttpClient
import org.readium.r2.streamer.PublicationOpener
import org.readium.r2.streamer.parser.DefaultPublicationParser

class BookReaderLifecycleStateViewModel : ViewModel() {
    var publication by mutableStateOf<Publication?>(null)
    var location by mutableStateOf<Locator?>(null)
    var locationUpdates by mutableStateOf(0)
}

class BookReaderLifecycleTestActivity : ComponentActivity() {
    private lateinit var readerState: BookReaderLifecycleStateViewModel

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        readerState = ViewModelProvider(this)[BookReaderLifecycleStateViewModel::class.java]
        setContent {
            MaterialTheme {
                val current = readerState.publication
                BookReaderContent(
                    state = if (current != null) {
                        BookReaderUiState(
                            publication = current,
                            lastLocation = readerState.location,
                        )
                    } else {
                        BookReaderUiState(isLoading = true)
                    },
                    onBack = {},
                    onRetry = {},
                    onLocationChanged = { locator ->
                        readerState.location = locator
                        readerState.locationUpdates++
                    },
                )
            }
        }
    }

    fun showPublication(value: Publication?) {
        readerState.publication = value
        if (value == null) {
            readerState.location = null
        }
    }

    fun currentLocation(): Locator? = readerState.location

    fun locationUpdateCount(): Int = readerState.locationUpdates
}

class BookReaderLifecycleTest {

    @get:Rule
    val composeRule = createAndroidComposeRule<BookReaderLifecycleTestActivity>()

    @Test
    fun readerRestoresLocationAfterActivityRecreationAndRealRotation() = runBlocking {
        val context = ApplicationProvider.getApplicationContext<Context>()
        val bookFile = File.createTempFile("mulletaflix-reader-lifecycle-", ".epub", context.cacheDir)
        bookFile.writeBytes(minimalEpub())
        val publication = openPublication(context, bookFile)

        try {
            composeRule.activityRule.scenario.onActivity { it.showPublication(publication) }
            composeRule.waitForIdle()
            composeRule.onNodeWithText("Livro de teste").assertExists()
            composeRule.waitForNextEnabled()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                currentLocation()?.locations?.progression != null
            }
            val initialLocation = requireNotNull(currentLocation())

            composeRule.onNodeWithText("Próximo").performClick()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                val current = currentLocationHref()
                val currentProgression = currentLocation()?.locations?.progression
                current == initialLocation.href.toString() &&
                    currentProgression != null &&
                    currentProgression > requireNotNull(initialLocation.locations.progression)
            }
            val advancedLocation = requireNotNull(currentLocation())
            val updatesBeforeRecreate = locationUpdateCount()

            composeRule.activityRule.scenario.recreate()

            composeRule.waitForIdle()
            composeRule.onNodeWithText("Livro de teste").assertExists()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                locationUpdateCount() > updatesBeforeRecreate &&
                    sameReadingPosition(currentLocation(), advancedLocation)
            }
            composeRule.waitForPreviousEnabled()

            val updatesBeforeRotation = locationUpdateCount()
            composeRule.activityRule.scenario.onActivity {
                it.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE
            }
            composeRule.waitUntil(timeoutMillis = 15_000) {
                composeRule.activity.resources.configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
            }
            try {
                composeRule.waitUntil(timeoutMillis = 20_000) {
                    locationUpdateCount() > updatesBeforeRotation &&
                        sameReadingPosition(
                            currentLocation(),
                            advancedLocation,
                            progressionTolerance = 0.05,
                        )
                }
            } catch (timeout: androidx.compose.ui.test.ComposeTimeoutException) {
                throw AssertionError(
                    "A posição não estabilizou após a rotação. " +
                        "Antes=$advancedLocation Depois=${currentLocation()} " +
                        "updatesAntes=$updatesBeforeRotation updatesDepois=${locationUpdateCount()}",
                    timeout,
                )
            }
            val rotatedLocation = currentLocation()
            assertTrue(
                "A rotação deve preservar a mesma posição de leitura. Antes=$advancedLocation Depois=$rotatedLocation",
                sameReadingPosition(rotatedLocation, advancedLocation, progressionTolerance = 0.05),
            )
            composeRule.waitForPreviousEnabled()
        } finally {
            composeRule.activityRule.scenario.onActivity { it.showPublication(null) }
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

    private fun minimalEpub(): ByteArray {
        val longChapter = (1..100).joinToString(separator = "") { index ->
            "<p>Parágrafo $index para validar restauração dentro do mesmo capítulo após recriação e rotação.</p>"
        }
        val output = ByteArrayOutputStream()
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
                        <dc:identifier id="pub-id">urn:uuid:mulletaflix-reader-lifecycle-test</dc:identifier>
                        <dc:title>Livro de teste</dc:title>
                        <dc:language>pt-BR</dc:language>
                        <meta property="dcterms:modified">2026-10-02T00:00:00Z</meta>
                      </metadata>
                      <manifest>
                        <item id="chapter1" href="chapter1.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter2" href="chapter2.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="chapter1"/><itemref idref="chapter2"/></spine>
                    </package>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/chapter1.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Capítulo 1</title></head>
                    <body><h1>Capítulo 1</h1>$longChapter</body></html>
                """.trimIndent(),
            )
            zip.writeEntry(
                "OEBPS/chapter2.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Capítulo 2</title></head>
                    <body><h1>Capítulo 2</h1><p>Continuação do teste de recriação.</p></body></html>
                """.trimIndent(),
            )
        }
        return output.toByteArray()
    }

    private fun androidx.compose.ui.test.junit4.AndroidComposeTestRule<*, *>.waitForNextEnabled() {
        waitUntil(timeoutMillis = 10_000) {
            runCatching {
                onNodeWithText("Próximo").assertIsEnabled()
                true
            }.getOrDefault(false)
        }
    }

    private fun androidx.compose.ui.test.junit4.AndroidComposeTestRule<*, *>.waitForPreviousEnabled() {
        waitUntil(timeoutMillis = 10_000) {
            runCatching {
                onNodeWithText("Anterior").assertIsEnabled()
                true
            }.getOrDefault(false)
        }
    }

    private fun currentLocation(): Locator? {
        var location: Locator? = null
        composeRule.activityRule.scenario.onActivity { location = it.currentLocation() }
        return location
    }

    private fun currentLocationHref(): String? = currentLocation()?.href?.toString()

    private fun sameReadingPosition(
        actual: Locator?,
        expected: Locator,
        progressionTolerance: Double = 0.02,
    ): Boolean {
        if (actual?.href != expected.href) return false
        if (actual.locations.position != expected.locations.position) return false
        val actualProgression = actual.locations.progression ?: return false
        val expectedProgression = expected.locations.progression ?: return false
        return abs(actualProgression - expectedProgression) <= progressionTolerance
    }

    private fun locationUpdateCount(): Int {
        var count = 0
        composeRule.activityRule.scenario.onActivity { count = it.locationUpdateCount() }
        return count
    }

    private fun ZipOutputStream.writeEntry(name: String, contents: String) {
        putNextEntry(ZipEntry(name))
        write(contents.toByteArray())
        closeEntry()
    }
}
