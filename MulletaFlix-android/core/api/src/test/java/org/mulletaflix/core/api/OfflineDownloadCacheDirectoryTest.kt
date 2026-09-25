package org.mulletaflix.core.api

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder

class OfflineDownloadCacheDirectoryTest {
    @get:Rule
    val temporaryFolder = TemporaryFolder()

    @Test
    fun `new installs use durable private directory`() {
        val cacheDirectory = temporaryFolder.newFolder("cache")
        val noBackupFilesDirectory = temporaryFolder.newFolder("no-backup")

        val resolved = resolveOfflineDownloadCacheDirectory(cacheDirectory, noBackupFilesDirectory)

        assertEquals(File(noBackupFilesDirectory, "downloads"), resolved)
    }

    @Test
    fun `legacy downloads move intact to durable directory`() {
        val cacheDirectory = temporaryFolder.newFolder("cache")
        val noBackupFilesDirectory = temporaryFolder.newFolder("no-backup")
        val legacyDirectory = File(cacheDirectory, "downloads").apply { mkdirs() }
        val legacyMedia = File(legacyDirectory, "downloaded-video.media").apply {
            writeBytes(byteArrayOf(1, 2, 3, 4))
        }

        val resolved = resolveOfflineDownloadCacheDirectory(cacheDirectory, noBackupFilesDirectory)

        val migratedMedia = File(resolved, legacyMedia.name)
        assertEquals(File(noBackupFilesDirectory, "downloads"), resolved)
        assertTrue(migratedMedia.isFile)
        assertTrue(migratedMedia.readBytes().contentEquals(byteArrayOf(1, 2, 3, 4)))
        assertFalse(legacyDirectory.exists())
    }

    @Test
    fun `legacy-only spans merge into an existing durable cache`() {
        val cacheDirectory = temporaryFolder.newFolder("cache")
        val noBackupFilesDirectory = temporaryFolder.newFolder("no-backup")
        val legacyMedia = File(cacheDirectory, "downloads/legacy.media").apply {
            parentFile?.mkdirs()
            writeText("legacy")
        }
        val persistentDirectory = File(noBackupFilesDirectory, "downloads").apply { mkdirs() }
        val persistentMedia = File(persistentDirectory, "current.media").apply { writeText("current") }

        val resolved = resolveOfflineDownloadCacheDirectory(cacheDirectory, noBackupFilesDirectory)

        assertEquals(persistentDirectory, resolved)
        assertEquals("current", persistentMedia.readText())
        assertEquals("legacy", File(persistentDirectory, legacyMedia.name).readText())
        assertFalse(legacyMedia.exists())
    }

    @Test
    fun `conflicting spans keep legacy cache active and preserve both files`() {
        val cacheDirectory = temporaryFolder.newFolder("cache")
        val noBackupFilesDirectory = temporaryFolder.newFolder("no-backup")
        val legacyMedia = File(cacheDirectory, "downloads/shared.media").apply {
            parentFile?.mkdirs()
            writeText("legacy")
        }
        val persistentMedia = File(noBackupFilesDirectory, "downloads/shared.media").apply {
            parentFile?.mkdirs()
            writeText("durable")
        }

        val resolved = resolveOfflineDownloadCacheDirectory(cacheDirectory, noBackupFilesDirectory)

        assertEquals(legacyMedia.parentFile, resolved)
        assertEquals("legacy", legacyMedia.readText())
        assertEquals("durable", persistentMedia.readText())
    }

    @Test
    fun `migration failure retains the legacy cache instead of losing downloads`() {
        val cacheDirectory = temporaryFolder.newFolder("cache")
        val noBackupFilesBlocker = File(temporaryFolder.root, "not-a-directory").apply { writeText("blocker") }
        val legacyMedia = File(cacheDirectory, "downloads/legacy.media").apply {
            parentFile?.mkdirs()
            writeText("preserve me")
        }

        val resolved = resolveOfflineDownloadCacheDirectory(cacheDirectory, noBackupFilesBlocker)

        assertEquals(legacyMedia.parentFile, resolved)
        assertEquals("preserve me", legacyMedia.readText())
    }
}
