package org.mulletaflix.android.service

import java.io.ByteArrayInputStream
import java.io.File
import java.io.IOException
import java.io.InputStream
import java.net.URI
import java.nio.file.Files
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata

class OfflineSubtitleStoreTest {
    private val root = Files.createTempDirectory("mulletaflix-offline-subtitles").toFile()
    private val store = OfflineSubtitleStore(File(root, "captions"), maxSubtitleBytes = 64)
    private val subtitle = DownloadSubtitleMetadata(12, "application/x-subrip", "pt-BR", "Português", true)

    @After
    fun cleanUp() {
        root.deleteRecursively()
    }

    @Test
    fun `stores complete subtitle at stable private uri`() {
        val bytes = "1\n00:00:00,100 --> 00:00:01,000\nOlá\n".toByteArray()
        val uri = store.store("server-a", "user-a", "download-a", subtitle, ByteArrayInputStream(bytes))

        assertEquals(uri, store.uriFor("server-a", "user-a", "download-a", subtitle))
        assertArrayEquals(bytes, File(URI(uri)).readBytes())
        assertTrue(File(URI(uri)).canonicalPath.startsWith(File(root, "captions").canonicalPath))
        assertEquals("srt", File(URI(uri)).extension)
    }

    @Test
    fun `subtitle file is isolated by user server and download`() {
        val uri = store.store("server-a", "user-a", "download-a", subtitle, ByteArrayInputStream(byteArrayOf(1, 2, 3)))

        assertEquals(uri, store.uriFor("server-a", "user-a", "download-a", subtitle))
        assertNull(store.uriFor("server-b", "user-a", "download-a", subtitle))
        assertNull(store.uriFor("server-a", "user-b", "download-a", subtitle))
        assertNull(store.uriFor("server-a", "user-a", "download-b", subtitle))
    }

    @Test
    fun `rejects empty oversized and html responses without leaving partial files`() {
        listOf(byteArrayOf(), ByteArray(65), "<!doctype html>login".toByteArray()).forEach { bytes ->
            val failure = runCatching {
                store.store("server-a", "user-a", "download-a", subtitle, ByteArrayInputStream(bytes))
            }.exceptionOrNull()
            assertTrue(failure is IOException)
        }

        assertNull(store.uriFor("server-a", "user-a", "download-a", subtitle))
        assertTrue(File(root, "captions").listFiles().isNullOrEmpty())
    }

    @Test
    fun `failed stream removes partial sidecar and preserves completed sidecars`() {
        val completedUri = store.store(
            "server-a",
            "user-a",
            "download-a",
            subtitle,
            ByteArrayInputStream("WEBVTT\n\n00:00:00.000 --> 00:00:01.000\nOlá".toByteArray()),
        )
        val interruptedSubtitle = subtitle.copy(streamIndex = 13)
        val failingStream = object : InputStream() {
            private var emitted = false

            override fun read(): Int = error("Bulk reads expected")

            override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
                if (emitted) throw IOException("Network stream interrupted")
                emitted = true
                buffer[offset] = '1'.code.toByte()
                return 1
            }
        }

        val failure = runCatching {
            store.store("server-a", "user-a", "download-a", interruptedSubtitle, failingStream)
        }.exceptionOrNull()

        assertTrue(failure is IOException)
        assertTrue(File(URI(completedUri)).isFile)
        assertNull(store.uriFor("server-a", "user-a", "download-a", interruptedSubtitle))
        assertTrue(File(root, "captions").listFiles().orEmpty().none { it.name.endsWith(".pending") })
    }

    @Test
    fun `removes only sidecar for matching scoped download`() {
        val uri = store.store("server-a", "user-a", "download-a", subtitle, ByteArrayInputStream(byteArrayOf(1, 2, 3)))
        store.store("server-a", "user-b", "download-a", subtitle, ByteArrayInputStream(byteArrayOf(4, 5, 6)))

        store.remove("server-a", "user-a", "download-a", listOf(subtitle))

        assertFalse(File(URI(uri)).exists())
        assertNull(store.uriFor("server-a", "user-a", "download-a", subtitle))
        assertTrue(store.uriFor("server-a", "user-b", "download-a", subtitle) != null)
    }

    @Test
    fun `enforces aggregate sidecar storage limit per download`() {
        val bounded = OfflineSubtitleStore(File(root, "bounded"), maxSubtitleBytes = 8, maxDownloadBytes = 5)
        val first = bounded.store("server-a", "user-a", "download-a", subtitle, ByteArrayInputStream(byteArrayOf(1, 2, 3)))
        val secondSubtitle = subtitle.copy(streamIndex = 13)
        val failure = runCatching {
            bounded.store("server-a", "user-a", "download-a", secondSubtitle, ByteArrayInputStream(byteArrayOf(4, 5, 6)))
        }.exceptionOrNull()

        assertTrue(failure is IOException)
        assertTrue(File(URI(first)).isFile)
        assertNull(bounded.uriFor("server-a", "user-a", "download-a", secondSubtitle))
    }
}
