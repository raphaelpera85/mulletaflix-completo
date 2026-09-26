package org.mulletaflix.data.repository

import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import okhttp3.ResponseBody
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
    fun `version transition from 1 0 99 to 1 1 0 is ordered correctly`() {
        assertTrue(repository.isVersionNewer("1.1.0", "1.0.99"))
        assertFalse(repository.isVersionNewer("1.0.99", "1.1.0"))
        assertTrue(repository.isSupportedAppVersion("1.1.0"))
        assertFalse(repository.isSupportedAppVersion("1.0.100"))
    }

    @Test
    fun `version cycles keep patch below 100 after minor rollover`() {
        assertTrue(repository.isSupportedAppVersion("1.1.99"))
        assertFalse(repository.isSupportedAppVersion("1.1.100"))
        assertTrue(repository.isVersionNewer("1.2.0", "1.1.99"))
        assertFalse(repository.isVersionNewer("1.1.100", "1.1.99"))
    }

    @Test
    fun `release parser ignores forbidden 1 0 100 and selects 1 1 0`() {
        val digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        val json = """
        [
            {
                "tag_name": "app-v1.0.100",
                "assets": [{
                    "name": "mulletaflix-app-v1.0.100.apk",
                    "digest": "sha256:$digest",
                    "browser_download_url": "https://github.com/releases/download/app-v1.0.100/app.apk"
                }]
            },
            {
                "tag_name": "app-v1.1.0",
                "assets": [{
                    "name": "mulletaflix-app-v1.1.0.apk",
                    "digest": "sha256:$digest",
                    "browser_download_url": "https://github.com/releases/download/app-v1.1.0/app.apk"
                }]
            }
        ]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.0.99")

        assertTrue(info.isUpdateAvailable)
        assertEquals("1.1.0", info.latestVersion)
        assertEquals("https://github.com/releases/download/app-v1.1.0/app.apk", info.apkDownloadUrl)
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

    @Test
    fun `a release whose APK has no digest is still offered`() {
        // GitHub sends an explicit `"digest": null` for an asset with no digest.
        // On Android, org.json's `optString(key, fallback)` returns the *string*
        // "null" for a JSON null, so the device treated the asset as having a
        // malformed digest and skipped the release entirely: the updater said
        // "already on the latest version" forever. The JVM org.json used by this
        // test returns the fallback instead, which is why the divergence was
        // invisible here — so `normalizedAssetDigest` normalises the literal for
        // both platforms and is asserted directly.
        val json = """
            [{
                "tag_name": "app-v1.4.0",
                "assets": [{
                    "name": "mulletaflix-app-v1.4.0.apk",
                    "browser_download_url": "https://github.com/releases/download/app-v1.4.0/mulletaflix-app-v1.4.0.apk",
                    "digest": null,
                    "size": 7000000
                }]
            }]
        """.trimIndent()

        val info = repository.parseReleases(json, "1.3.9")

        assertTrue("a release without a digest must still be offered", info.isUpdateAvailable)
        assertEquals("1.4.0", info.latestVersion)
        assertEquals(null, info.apkSha256)
    }

    @Test
    fun `the literal null digest is treated as absent`() {
        // What AOSP's org.json actually hands back for `"digest": null`.
        val asset = org.json.JSONObject("""{"name":"a.apk","digest":null}""")
        val explicitNull = org.json.JSONObject("""{"name":"a.apk","digest":"null"}""")
        val valid = org.json.JSONObject(
            """{"name":"a.apk","digest":"sha256:${"a".repeat(64)}"}""",
        )
        val blank = org.json.JSONObject("""{"name":"a.apk","digest":"   "}""")

        assertEquals("", normalizedAssetDigest(asset))
        assertEquals("", normalizedAssetDigest(explicitNull))
        assertEquals("", normalizedAssetDigest(blank))
        assertEquals("sha256:${"a".repeat(64)}", normalizedAssetDigest(valid))
    }

    /**
     * Um corpo que a função **não conseguiu ler** não é a mesma coisa que "não há
     * release nova". Antes, a exceção de parse era engolida e a função devolvia
     * sucesso com `isUpdateAvailable = false`, então Ajustes dizia "Você já está na
     * versão mais recente" — uma afirmação sobre uma resposta que ninguém leu.
     */
    @Test
    fun `an unreadable body is a failure, not a claim of being up to date`() {
        val thrown = runCatching { repository.parseReleases("nao e json", "1.2.72") }

        assertTrue(
            "uma resposta ilegível precisa virar erro, não \"já está atualizado\"",
            thrown.isFailure,
        )
    }

    @Test
    fun `a well formed but empty list still means no update`() {
        val info = repository.parseReleases("[]", "1.2.72")

        assertFalse(info.isUpdateAvailable)
        assertEquals("1.2.72", info.latestVersion)
    }

    @Test
    fun `an unsuccessful GitHub response is closed`() {
        val body = mockk<ResponseBody>(relaxed = true)
        val response = httpResponse(code = 503, body = body)

        val result = repository.parseResponse(response, "1.3.66")

        assertTrue(result.isFailure)
        assertEquals("Falha ao consultar releases: HTTP 503", result.exceptionOrNull()?.message)
        verify(exactly = 1) { body.close() }
    }

    @Test
    fun `a successful GitHub response is closed after parsing`() {
        val body = mockk<ResponseBody>(relaxed = true)
        every { body.string() } returns "[]"
        val response = httpResponse(code = 200, body = body)

        val result = repository.parseResponse(response, "1.3.66")

        assertTrue(result.isSuccess)
        assertFalse(result.getOrThrow().isUpdateAvailable)
        verify(exactly = 1) { body.close() }
    }

    @Test
    fun `a GitHub response is closed when its body cannot be parsed`() {
        val body = mockk<ResponseBody>(relaxed = true)
        every { body.string() } returns "not json"
        val response = httpResponse(code = 200, body = body)

        val result = runCatching { repository.parseResponse(response, "1.3.66") }

        assertTrue(result.isFailure)
        verify(exactly = 1) { body.close() }
    }

    private fun httpResponse(code: Int, body: ResponseBody): Response =
        Response.Builder()
            .request(Request.Builder().url("https://api.github.com/releases").build())
            .protocol(Protocol.HTTP_1_1)
            .code(code)
            .message("test response")
            .body(body)
            .build()
}
