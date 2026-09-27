package org.mulletaflix.android.service

import java.io.ByteArrayInputStream
import java.io.File
import java.io.IOException
import java.net.URI
import java.nio.file.Files
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class OfflineArtworkStoreTest {
    private val root = Files.createTempDirectory("mulletaflix-offline-artwork").toFile()
    private val store = OfflineArtworkStore(File(root, "art"), maxArtworkBytes = 8)

    @After
    fun cleanUp() {
        root.deleteRecursively()
    }

    @Test
    fun `persists artwork under a stable private file uri`() {
        val uri = store.store("user:movie-1", ByteArrayInputStream(PNG_BYTES), "png")

        assertEquals(uri, store.uriFor("user:movie-1"))
        assertArrayEquals(PNG_BYTES, File(URI(uri)).readBytes())
        assertTrue(File(URI(uri)).canonicalPath.startsWith(File(root, "art").canonicalPath))
    }

    @Test
    fun `replaces old artwork only after the new bytes are complete`() {
        val oldUri = store.store("movie-1", ByteArrayInputStream(byteArrayOf(1, 2, 3)), "png")
        val newBytes = byteArrayOf(4, 5, 6, 7)
        val newUri = store.store("movie-1", ByteArrayInputStream(newBytes), "webp")

        assertFalse(File(URI(oldUri)).exists())
        assertEquals(newUri, store.uriFor("movie-1"))
        assertArrayEquals(newBytes, File(URI(newUri)).readBytes())
    }

    @Test
    fun `rejects oversized partial images and removes their temporary file`() {
        val failure = runCatching {
            store.store("movie-2", ByteArrayInputStream(ByteArray(9)), "jpg")
        }.exceptionOrNull()

        assertTrue(failure is IOException)
        assertNull(store.uriFor("movie-2"))
        assertTrue(File(root, "art").listFiles().isNullOrEmpty())
    }

    @Test
    fun `removes persisted artwork for a download`() {
        val uri = store.store("movie-3", ByteArrayInputStream(byteArrayOf(1, 2, 3)), "jpg")

        store.remove("movie-3")

        assertFalse(File(URI(uri)).exists())
        assertNull(store.uriFor("movie-3"))
    }

    @Test
    fun `only recognized image content types receive image extensions`() {
        assertEquals("jpg", artworkExtensionForContentType("image/jpeg"))
        assertEquals("webp", artworkExtensionForContentType("image/webp"))
        assertNull(artworkExtensionForContentType("application/octet-stream"))
    }

    private companion object {
        val PNG_BYTES = byteArrayOf(0x89.toByte(), 0x50, 0x4e, 0x47)
    }
}
