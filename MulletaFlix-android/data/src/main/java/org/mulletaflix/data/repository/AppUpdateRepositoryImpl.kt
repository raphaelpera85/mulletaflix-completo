package org.mulletaflix.data.repository

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONArray
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.repository.AppUpdateRepository
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AppUpdateRepositoryImpl @Inject constructor() : AppUpdateRepository {

    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .build()

    companion object {
        private const val GITHUB_RELEASES_URL =
            "https://api.github.com/repos/raphaelpera85/mulletaflix-completo/releases?per_page=100"
        private const val USER_AGENT = "MulletaFlix-Android-App"
    }

    override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> =
        withContext(Dispatchers.IO) {
            try {
                val request = Request.Builder()
                    .url(GITHUB_RELEASES_URL)
                    .header("User-Agent", USER_AGENT)
                    .header("Accept", "application/vnd.github.v3+json")
                    .get()
                    .build()

                val response = httpClient.newCall(request).execute()
                if (!response.isSuccessful) {
                    return@withContext Result.failure(
                        Exception("Falha ao consultar releases: HTTP ${response.code}")
                    )
                }

                val responseBody = response.body?.string()
                    ?: return@withContext Result.failure(Exception("Resposta vazia do GitHub"))

                val updateInfo = parseReleases(responseBody, currentVersion)
                Result.success(updateInfo)
            } catch (e: Exception) {
                Result.failure(e)
            }
        }

    internal fun parseReleases(jsonString: String, currentVersion: String): AppUpdateInfo {
        var highestVersionStr: String = currentVersion
        var bestApkUrl: String? = null
        var bestNotes: String? = null
        var bestSha256: String? = null
        var bestSize: Long = 0L
        var bestPublishedAt: String? = null

        // Um corpo que não é JSON — ou que mudou de forma — **não** é "nenhuma
        // release nova". A exceção era engolida aqui e a função devolvia sucesso com
        // `isUpdateAvailable = false`, então a tela de Ajustes afirmava "Você já está
        // na versão mais recente" para uma resposta que ela não conseguiu ler. Agora
        // a falha sobe para `checkForUpdate` e vira `updateErrorMessage`.
        //
        // A tolerância por release continua: `optJSONObject`/`optString`/`optBoolean`
        // ignoram um item estranho no meio da lista, que é o caso legítimo.
        val releasesArray = JSONArray(jsonString)
        for (i in 0 until releasesArray.length()) {
            val release = releasesArray.optJSONObject(i) ?: continue
            val tagName = release.optString("tag_name", "").trim()

            // Considerar exclusivamente releases dedicadas do aplicativo Android
            if (!tagName.startsWith("app-", ignoreCase = true)) {
                continue
            }
            // Do not offer preview or draft artifacts through the in-app updater.
            // The APK must come from an official, published stable release.
            if (release.optBoolean("draft", false) || release.optBoolean("prerelease", false)) {
                continue
            }

            val body = release.optString("body", "").trim()
            val publishedAt = release.optString("published_at", "").trim()
            val assets = release.optJSONArray("assets") ?: continue

            var releaseApkUrl: String? = null
            var releaseApkSha256: String? = null
            var releaseApkDigestInvalid = false
            var releaseApkSize: Long = 0L
            var assetVersion: String? = null

            for (j in 0 until assets.length()) {
                val asset = assets.optJSONObject(j) ?: continue
                val name = asset.optString("name", "")
                if (name.endsWith(".apk", ignoreCase = true)) {
                    releaseApkUrl = asset.optString("browser_download_url", "")
                    val rawDigest = normalizedAssetDigest(asset)
                    releaseApkSha256 = rawDigest
                        .removePrefix("sha256:")
                        .trim()
                        .takeIf { it.matches(Regex("[0-9a-fA-F]{64}")) }
                    releaseApkDigestInvalid = rawDigest.isNotBlank() && releaseApkSha256 == null
                    releaseApkSize = asset.optLong("size", 0L)
                    // Extract version from asset name like mulletaflix-app-v1.0.0.apk
                    val match = Regex("""(?:mulletaflix-app-)?v?([0-9]+(?:\.[0-9]+)*)\.apk""", RegexOption.IGNORE_CASE)
                        .find(name)
                    if (match != null) {
                        assetVersion = match.groupValues[1]
                    }
                    break
                }
            }

            if (releaseApkUrl.isNullOrBlank() || releaseApkDigestInvalid) {
                continue
            }

            val candidateVersion = when {
                !assetVersion.isNullOrBlank() -> assetVersion
                tagName.startsWith("app-v", ignoreCase = true) -> tagName.substring(5)
                tagName.startsWith("app-", ignoreCase = true) -> tagName.substring(4)
                else -> tagName
            }

            // The Android channel uses two-digit patch cycles. The first
            // release after 1.0.99 is 1.1.0, never 1.0.100.
            if (!isSupportedAppVersion(candidateVersion)) continue

            if (isVersionNewer(candidateVersion, highestVersionStr)) {
                highestVersionStr = candidateVersion
                bestApkUrl = releaseApkUrl
                bestSha256 = releaseApkSha256
                bestNotes = body.ifBlank { null }
                bestSize = releaseApkSize
                bestPublishedAt = publishedAt.ifBlank { null }
            }
        }

        val isUpdateAvailable = isVersionNewer(highestVersionStr, currentVersion)

        return AppUpdateInfo(
            isUpdateAvailable = isUpdateAvailable,
            currentVersion = currentVersion,
            latestVersion = highestVersionStr,
            releaseNotes = bestNotes,
            apkDownloadUrl = bestApkUrl,
            apkSha256 = bestSha256,
            apkSize = bestSize,
            publishedAt = bestPublishedAt,
        )
    }

    internal fun parseVersion(versionStr: String): List<Int> {
        val clean = versionStr
            .substringBefore("-")
            .trimStart('a', 'p', 'v', 'V', '-')
            .trim()
        return clean.split(".").mapNotNull { it.toIntOrNull() }
    }

    internal fun isVersionNewer(remote: String, current: String): Boolean {
        val remoteParts = parseVersion(remote)
        val currentParts = parseVersion(current)
        if (remoteParts.isEmpty()) return false
        // Release version names use two-digit patch cycles. A remote
        // 1.0.100-style value must never outrank a valid SemVer release.
        if (remoteParts.size == 3 && remoteParts[2] !in 0..99) return false
        if (currentParts.isEmpty()) return true

        val maxLen = maxOf(remoteParts.size, currentParts.size)
        for (i in 0 until maxLen) {
            val r = remoteParts.getOrElse(i) { 0 }
            val c = currentParts.getOrElse(i) { 0 }
            if (r > c) return true
            if (r < c) return false
        }
        return false
    }

    internal fun isSupportedAppVersion(version: String): Boolean {
        val parts = parseVersion(version)
        return parts.size == 3 && parts[2] in 0..99
    }
}

/**
 * The asset's `digest` field, with JSON null normalised to "absent".
 *
 * GitHub sends an explicit `"digest": null` for an asset that has no digest. On
 * Android, `org.json`'s `JSONObject.optString(key, fallback)` returns the **string
 * `"null"`** for a JSON null rather than the fallback — AOSP implements it as
 * `JSON.toString(opt(name))`, and `JSON.toString` is `String.valueOf` for any
 * non-null object. The plain JVM `org.json` used by the unit tests returns the
 * fallback instead, so the two platforms disagreed and only the device took the
 * wrong branch: `"null"` is not a valid sha256, the release was treated as
 * corrupt and skipped, and the in-app updater reported "already on the latest
 * version" forever.
 *
 * The literal is normalised here so both platforms behave the same, and the
 * "no digest" case keeps working as the older releases intend.
 */
internal fun normalizedAssetDigest(asset: org.json.JSONObject): String =
    asset.optString("digest", "")
        .trim()
        .takeUnless { it.isEmpty() || it.equals("null", ignoreCase = true) }
        .orEmpty()
