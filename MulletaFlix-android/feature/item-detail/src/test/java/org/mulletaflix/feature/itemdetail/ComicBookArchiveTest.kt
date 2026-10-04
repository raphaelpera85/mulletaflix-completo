package org.mulletaflix.feature.itemdetail

import java.io.File
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class ComicBookArchiveTest {
    @Test
    fun `lists supported images in natural page order and skips metadata`() {
        val file = createArchive("pages/10.jpg", "ComicInfo.xml", "pages/2.PNG", "pages/1.webp", "notes.txt")

        try {
            val archive = ComicBookArchive.open(file)

            assertEquals(3, archive.pageCount)
            assertEquals("pages/1.webp", archive.pageName(0))
            assertEquals("pages/2.PNG", archive.pageName(1))
            assertEquals("pages/10.jpg", archive.pageName(2))
        } finally {
            file.delete()
        }
    }

    @Test
    fun `rejects archive without supported image pages`() {
        val file = createArchive("ComicInfo.xml", "notes.txt")

        try {
            val failure = assertThrows(java.io.IOException::class.java) { ComicBookArchive.open(file) }
            assertTrue(failure.message.orEmpty().contains("no supported pages"))
        } finally {
            file.delete()
        }
    }

    @Test
    fun `rejects corrupt and empty archives`() {
        val corrupt = File.createTempFile("comic-corrupt-", ".cbz").apply { writeText("not a zip") }
        val empty = File.createTempFile("comic-empty-", ".cbz")

        try {
            assertThrows(Exception::class.java) { ComicBookArchive.open(corrupt) }
            val failure = assertThrows(java.io.IOException::class.java) { ComicBookArchive.open(empty) }
            assertTrue(failure.message.orEmpty().contains("empty or missing"))
        } finally {
            corrupt.delete()
            empty.delete()
        }
    }

    private fun createArchive(vararg names: String): File {
        val file = File.createTempFile("comic-archive-", ".cbz")
        ZipOutputStream(file.outputStream()).use { archive ->
            names.forEach { name ->
                archive.putNextEntry(ZipEntry(name))
                if (!name.endsWith('/')) archive.write(byteArrayOf(1, 2, 3))
                archive.closeEntry()
            }
        }
        return file
    }
}
