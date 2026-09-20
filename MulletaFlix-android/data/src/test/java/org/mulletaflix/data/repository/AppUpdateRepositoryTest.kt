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
        assertTrue(repository.isVersionNewer("1.0.1", "1.0.0"))
        assertTrue(repository.isVersionNewer("1.1.0", "1.0.0"))
        assertTrue(repository.isVersionNewer("2.0.0", "1.0.0"))
        assertTrue(repository.isVersionNewer("1.0.0.1", "1.0.0"))

        assertFalse(repository.isVersionNewer("1.0.0", "1.0.0"))
        assertFalse(repository.isVersionNewer("0.9.9", "1.0.0"))
    }

    @Test
    fun `parseReleases detects newer APK and populates metadata`() {
        val json = """
        [
            {
                "tag_name": "app-v1.0.1",
                "body": "Novas correções e melhorias no player",
                "published_at": "2026-09-18T05:00:00Z",
                "assets": [
                    {
                        "name": "mulletaflix-app-v1.0.1.apk",
                        "size": 7500000,
                        "digest": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                        "browser_download_url": "https://github.com/releases/download/app-v1.0.1/mulletaflix-app-v1.0.1.apk"
                    }
                ]
            },
            {
                "tag_name": "app-v1.0.0",
                "body": "Versão inicial",
                "published_at": "2026-09-18T03:00:00Z",
                "assets": [
                    {
                        "name": "mulletaflix-app-v1.0.0.apk",
                        "size": 7300000,
                        "browser_download_url": "https://github.com/releases/download/app-v1.0.0/mulletaflix-app-v1.0.0.apk"
                    }
                ]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.0.0")

        assertTrue(info.isUpdateAvailable)
        assertEquals("1.0.1", info.latestVersion)
        assertEquals("1.0.0", info.currentVersion)
        assertEquals("Novas correções e melhorias no player", info.releaseNotes)
        assertEquals("https://github.com/releases/download/app-v1.0.1/mulletaflix-app-v1.0.1.apk", info.apkDownloadUrl)
        assertEquals("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", info.apkSha256)
        assertEquals(7500000L, info.apkSize)
    }

    @Test
    fun `parseReleases ignores server releases completely`() {
        val json = """
        [
            {
                "tag_name": "v12.0.3",
                "body": "Server release",
                "assets": [
                    {
                        "name": "mulletaflix-update-win-x64.zip",
                        "size": 350000000,
                        "browser_download_url": "https://github.com/.../server.zip"
                    }
                ]
            },
            {
                "tag_name": "v12.0.2",
                "body": "Server release older",
                "assets": [
                    {
                        "name": "mulletaflix-update-win-x64.zip",
                        "size": 350000000,
                        "browser_download_url": "https://github.com/.../server.zip"
                    }
                ]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.0.0")

        assertFalse(info.isUpdateAvailable)
        assertEquals("1.0.0", info.latestVersion)
        assertEquals("1.0.0", info.currentVersion)
    }

    @Test
    fun `parseReleases returns no update when installed app version is latest`() {
        val json = """
        [
            {
                "tag_name": "app-v1.0.0",
                "body": "Versão estável",
                "assets": [
                    {
                        "name": "mulletaflix-app-v1.0.0.apk",
                        "size": 7300000,
                        "browser_download_url": "https://github.com/.../app.apk"
                    }
                ]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.0.0")

        assertFalse(info.isUpdateAvailable)
        assertEquals("1.0.0", info.latestVersion)
        assertEquals("1.0.0", info.currentVersion)
    }

    @Test
    fun `parseReleases ignores draft and prerelease APKs`() {
        val json = """
        [
            {
                "tag_name": "app-v9.0.0-preview",
                "prerelease": true,
                "assets": [
                    {
                        "name": "mulletaflix-app-v9.0.0.apk",
                        "browser_download_url": "https://github.com/releases/download/app-v9.0.0-preview/mulletaflix-app-v9.0.0.apk"
                    }
                ]
            },
            {
                "tag_name": "app-v8.0.0-draft",
                "draft": true,
                "assets": [
                    {
                        "name": "mulletaflix-app-v8.0.0.apk",
                        "browser_download_url": "https://github.com/releases/download/app-v8.0.0-draft/mulletaflix-app-v8.0.0.apk"
                    }
                ]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.0.0")

        assertFalse(info.isUpdateAvailable)
        assertEquals("1.0.0", info.latestVersion)
        assertEquals(null, info.apkDownloadUrl)
    }

    @Test
    fun `parseReleases ignores APK when published digest is malformed`() {
        val json = """
        [{
            "tag_name": "app-v9.0.0",
            "assets": [{
                "name": "mulletaflix-app-v9.0.0.apk",
                "digest": "sha256:not-a-valid-sha256",
                "browser_download_url": "https://github.com/releases/download/app-v9.0.0/mulletaflix-app-v9.0.0.apk"
            }]
        }]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.0.0")

        assertFalse(info.isUpdateAvailable)
        assertEquals("1.0.0", info.latestVersion)
        assertEquals(null, info.apkDownloadUrl)
        assertEquals(null, info.apkSha256)
    }

    @Test
    fun `parseReleases keeps the Android channel isolated from server releases`() {
        val serverReleases = (1..35).joinToString(",") { index ->
            """
            {
                "tag_name": "v12.0.$index",
                "assets": [{
                    "name": "mulletaflix-update-win-x64.zip",
                    "browser_download_url": "https://github.com/releases/download/v12.0.$index/server.zip"
                }]
            }
            """.trimIndent()
        }
        val json = """
            [$serverReleases,
              {
                "tag_name": "app-v1.0.38",
                "assets": [{
                    "name": "mulletaflix-app-v1.0.38.apk",
                    "browser_download_url": "https://github.com/releases/download/app-v1.0.38/mulletaflix-app-v1.0.38.apk"
                }]
              }]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.0.37")

        assertTrue(info.isUpdateAvailable)
        assertEquals("1.0.38", info.latestVersion)
        assertEquals(
            "https://github.com/releases/download/app-v1.0.38/mulletaflix-app-v1.0.38.apk",
            info.apkDownloadUrl,
        )
    }
}
