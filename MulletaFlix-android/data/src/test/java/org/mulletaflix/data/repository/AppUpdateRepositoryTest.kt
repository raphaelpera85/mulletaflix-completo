package org.mulletaflix.data.repository

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class AppUpdateRepositoryTest {

    private lateinit var repository: AppUpdateRepositoryImpl

    @Before
    fun setUp() {
        repository = AppUpdateRepositoryImpl()
    }

    @Test
    fun `isVersionNewer correctly compares semantic versions`() {
        assertTrue(repository.isVersionNewer("12.0.3", "12.0.2"))
        assertTrue(repository.isVersionNewer("12.1.0", "12.0.2"))
        assertTrue(repository.isVersionNewer("13.0.0", "12.0.2"))
        assertTrue(repository.isVersionNewer("12.0.2.1", "12.0.2"))

        assertFalse(repository.isVersionNewer("12.0.2", "12.0.2"))
        assertFalse(repository.isVersionNewer("12.0.1", "12.0.2"))
        assertFalse(repository.isVersionNewer("11.9.9", "12.0.2"))
    }

    @Test
    fun `parseReleases detects newer APK and populates metadata`() {
        val json = """
        [
            {
                "tag_name": "app-v12.0.3",
                "body": "Novas correções e melhorias no player",
                "published_at": "2026-09-18T05:00:00Z",
                "assets": [
                    {
                        "name": "mulletaflix-app-v12.0.3.apk",
                        "size": 7500000,
                        "browser_download_url": "https://github.com/releases/download/app-v12.0.3/mulletaflix-app-v12.0.3.apk"
                    }
                ]
            },
            {
                "tag_name": "app-v12.0.2",
                "body": "Versão anterior",
                "published_at": "2026-09-18T03:00:00Z",
                "assets": [
                    {
                        "name": "mulletaflix-app-v12.0.2.apk",
                        "size": 7300000,
                        "browser_download_url": "https://github.com/releases/download/app-v12.0.2/mulletaflix-app-v12.0.2.apk"
                    }
                ]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "12.0.2")

        assertTrue(info.isUpdateAvailable)
        assertEquals("12.0.3", info.latestVersion)
        assertEquals("12.0.2", info.currentVersion)
        assertEquals("Novas correções e melhorias no player", info.releaseNotes)
        assertEquals("https://github.com/releases/download/app-v12.0.3/mulletaflix-app-v12.0.3.apk", info.apkDownloadUrl)
        assertEquals(7500000L, info.apkSize)
    }

    @Test
    fun `parseReleases returns no update when installed version is latest`() {
        val json = """
        [
            {
                "tag_name": "v12.0.2",
                "body": "Versão estável",
                "assets": [
                    {
                        "name": "mulletaflix-app-v12.0.2.apk",
                        "size": 7300000,
                        "browser_download_url": "https://github.com/.../app.apk"
                    }
                ]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "12.0.2")

        assertFalse(info.isUpdateAvailable)
        assertEquals("12.0.2", info.latestVersion)
        assertEquals("12.0.2", info.currentVersion)
    }

    @Test
    fun `parseReleases ignores releases without APK asset`() {
        val json = """
        [
            {
                "tag_name": "v12.0.9",
                "body": "Server only release",
                "assets": [
                    {
                        "name": "server-update.zip",
                        "size": 100000000,
                        "browser_download_url": "https://github.com/.../server.zip"
                    }
                ]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "12.0.2")

        assertFalse(info.isUpdateAvailable)
        assertEquals("12.0.2", info.latestVersion)
    }
}
