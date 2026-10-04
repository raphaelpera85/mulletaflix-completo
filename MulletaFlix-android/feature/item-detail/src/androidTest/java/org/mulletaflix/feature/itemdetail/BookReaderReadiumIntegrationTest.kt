package org.mulletaflix.feature.itemdetail

import android.app.Application
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runBlocking
import org.json.JSONObject
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.readium.navigator.web.reflowable.ReflowableWebConfiguration
import org.readium.navigator.web.reflowable.ReflowableWebGoLocation
import org.readium.navigator.web.reflowable.ReflowableWebRendition
import org.readium.navigator.web.reflowable.ReflowableWebRenditionFactory
import org.readium.navigator.web.reflowable.preferences.ReflowableWebPreferences
import org.readium.r2.shared.publication.Locator
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class BookReaderReadiumIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    private val context = InstrumentationRegistry.getInstrumentation().targetContext

    @Test
    fun opensValidEpubAndReadsItsMetadataWithProductionParser() {
        val epub = createEpub()

        try {
            val publication = open(epub)

            assertEquals("Integration Test Book", publication.metadata.title)
            assertEquals(2, publication.readingOrder.size)
        } finally {
            epub.delete()
        }
    }

    @Test
    fun convertsMalformedEpubParserAssertionIntoReaderFailure() {
        val invalidEpub = File.createTempFile("invalid-book-", ".epub", context.cacheDir)
        ZipOutputStream(invalidEpub.outputStream()).use { zip ->
            val mimetype = "application/epub+zip".toByteArray(Charsets.US_ASCII)
            val entry = ZipEntry("mimetype").apply {
                method = ZipEntry.STORED
                size = mimetype.size.toLong()
                compressedSize = mimetype.size.toLong()
                crc = CRC32().apply { update(mimetype) }.value
            }
            zip.putNextEntry(entry)
            zip.write(mimetype)
            zip.closeEntry()
            zip.writeEntry(
                "META-INF/container.xml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                      <rootfiles><rootfile full-path="OPS/package.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>""".trimIndent(),
            )
            zip.writeEntry("OPS/package.opf", "<package><metadata>")
        }

        try {
            val failure = assertThrows(IllegalArgumentException::class.java) { open(invalidEpub) }
            assertTrue(failure.cause is AssertionError)
        } finally {
            invalidEpub.delete()
        }
    }

    @Test
    fun readingProgressPersistsAcrossStoreInstancesAndIsolatedByAccountAndServer() = runBlocking {
        val store = BookReaderProgressStore(context)
        val reloadedStore = BookReaderProgressStore(context)
        val scope = HomeFeedCacheScope("server-progress-test", "https://server.example", "user-progress-test")
        val differentAccount = scope.copy(userId = "another-user")
        val differentServer = scope.copy(serverId = "another-server")
        val sameServerLanEndpoint = scope.copy(serverUrl = "http://192.168.1.20:8096")
        val itemId = "book-progress-${System.nanoTime()}"
        val locator = locator("OPS/chapter-2.xhtml", 0.42)
        val urlOnlyScope = scope.copy(serverId = null, serverUrl = "https://server.example/")
        val normalizedUrlScope = urlOnlyScope.copy(serverUrl = "https://server.example")
        val urlItemId = "$itemId-url"

        try {
            store.write(scope, itemId, locator)

            assertEquals(locator.toJSON().toString(), reloadedStore.read(scope, itemId)?.toJSON().toString())
            assertEquals(locator.toJSON().toString(), reloadedStore.read(sameServerLanEndpoint, itemId)?.toJSON().toString())
            assertEquals(null, reloadedStore.read(differentAccount, itemId))
            assertEquals(null, reloadedStore.read(differentServer, itemId))
            store.write(urlOnlyScope, urlItemId, locator)
            assertEquals(locator.toJSON().toString(), reloadedStore.read(normalizedUrlScope, urlItemId)?.toJSON().toString())
            store.write(differentAccount, itemId, locator)
            reloadedStore.remove(scope, itemId)
            assertEquals(null, store.read(scope, itemId))
            assertEquals(locator.toJSON().toString(), store.read(differentAccount, itemId)?.toJSON().toString())
        } finally {
            reloadedStore.remove(scope, itemId)
            reloadedStore.remove(urlOnlyScope, urlItemId)
        }
    }

    @Test
    fun comicPageProgressPersistsAndRemainsScopedToItsAccountAndServer() = runBlocking {
        val store = BookReaderProgressStore(context)
        val reloadedStore = BookReaderProgressStore(context)
        val scope = HomeFeedCacheScope("comic-server-test", "https://comic.example", "comic-user-test")
        val otherAccount = scope.copy(userId = "other-comic-user")
        val otherServer = scope.copy(serverId = "other-comic-server")
        val itemId = "comic-progress-${System.nanoTime()}"
        val locator = ComicBookArchive.locatorForPage(index = 4, pageCount = 12)

        try {
            store.write(scope, itemId, locator)

            val restored = reloadedStore.read(scope, itemId)
            assertEquals(4, ComicBookArchive.pageIndexFromLocator(restored, pageCount = 12))
            assertEquals(null, reloadedStore.read(otherAccount, itemId))
            assertEquals(null, reloadedStore.read(otherServer, itemId))
        } finally {
            reloadedStore.remove(scope, itemId)
        }
    }

    @Test
    fun reflowableRenditionRestoresSavedLocationAfterRecreation() {
        val epub = createEpub()
        try {
            val publication = open(epub)
            val savedLocator = locator("OPS/chapter-2.xhtml", 0.42)
            val renditionState = runBlocking(Dispatchers.IO) {
                ReflowableWebRenditionFactory(
                    application = context.applicationContext as Application,
                    publication = publication,
                    configuration = ReflowableWebConfiguration(),
                )?.createRenditionState(
                    initialPreferences = ReflowableWebPreferences(),
                    initialLocation = ReflowableWebGoLocation(savedLocator),
                )?.getOrNull()
            }
            val validRenditionState = requireNotNull(renditionState)

            composeRule.setContent {
                ReflowableWebRendition(state = validRenditionState, modifier = Modifier.fillMaxSize())
            }
            composeRule.waitUntil(timeoutMillis = 10_000) {
                validRenditionState.controller?.location?.href?.toString() == "OPS/chapter-2.xhtml"
            }
            assertEquals("OPS/chapter-2.xhtml", validRenditionState.controller?.location?.href?.toString())
        } finally {
            epub.delete()
        }
    }

    private fun open(file: File) = runBlocking(Dispatchers.IO) {
        openBookPublication(context, file)
    }

    private fun createEpub(): File {
        val file = File.createTempFile("valid-book-", ".epub", context.cacheDir)
        ZipOutputStream(file.outputStream()).use { zip ->
            val mimetype = "application/epub+zip".toByteArray(Charsets.US_ASCII)
            val mimetypeEntry = ZipEntry("mimetype").apply {
                method = ZipEntry.STORED
                size = mimetype.size.toLong()
                compressedSize = mimetype.size.toLong()
                crc = CRC32().apply { update(mimetype) }.value
            }
            zip.putNextEntry(mimetypeEntry)
            zip.write(mimetype)
            zip.closeEntry()

            zip.writeEntry(
                "META-INF/container.xml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                      <rootfiles><rootfile full-path="OPS/package.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/package.opf",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:identifier id="book-id">urn:uuid:mulletaflix-readium-test</dc:identifier>
                        <dc:title>Integration Test Book</dc:title>
                        <dc:language>pt-BR</dc:language>
                      </metadata>
                      <manifest>
                        <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter-2" href="chapter-2.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="chapter"/><itemref idref="chapter-2"/></spine>
                    </package>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/chapter.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
                      <head><title>Capítulo</title></head>
                      <body><h1>Leitura funcionando</h1><p>Conteúdo EPUB mínimo válido.</p></body>
                    </html>""".trimIndent(),
            )
            zip.writeEntry(
                "OPS/chapter-2.xhtml",
                """<?xml version="1.0" encoding="UTF-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml" lang="pt-BR">
                      <head><title>Capítulo dois</title></head>
                      <body><h1>Retomada funcionando</h1><p>Posição salva restaurada.</p></body>
                    </html>""".trimIndent(),
            )
        }
        return file
    }

    private fun locator(href: String, progression: Double) = requireNotNull(
        Locator.fromJSON(
            JSONObject(
                """{"href":"$href","type":"application/xhtml+xml","locations":{"progression":$progression}}""",
            ),
        ),
    )

    private fun ZipOutputStream.writeEntry(path: String, content: String) {
        putNextEntry(ZipEntry(path))
        write(content.toByteArray(Charsets.UTF_8))
        closeEntry()
    }
}
