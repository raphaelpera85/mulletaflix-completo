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
        var bestSize: Long = 0L
        var bestPublishedAt: String? = null

        try {
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
                var releaseApkSize: Long = 0L
                var assetVersion: String? = null

                for (j in 0 until assets.length()) {
                    val asset = assets.optJSONObject(j) ?: continue
                    val name = asset.optString("name", "")
                    if (name.endsWith(".apk", ignoreCase = true)) {
                        releaseApkUrl = asset.optString("browser_download_url", "")
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

                if (releaseApkUrl.isNullOrBlank()) {
                    continue
                }

                val candidateVersion = when {
                    !assetVersion.isNullOrBlank() -> assetVersion
                    tagName.startsWith("app-v", ignoreCase = true) -> tagName.substring(5)
                    tagName.startsWith("app-", ignoreCase = true) -> tagName.substring(4)
                    else -> tagName
                }

                if (isVersionNewer(candidateVersion, highestVersionStr)) {
                    highestVersionStr = candidateVersion
                    bestApkUrl = releaseApkUrl
                    bestNotes = body.ifBlank { null }
                    bestSize = releaseApkSize
                    bestPublishedAt = publishedAt.ifBlank { null }
                }
            }
        } catch (_: Exception) {
            // Best effort JSON parsing
        }

        val isUpdateAvailable = isVersionNewer(highestVersionStr, currentVersion)

        return AppUpdateInfo(
            isUpdateAvailable = isUpdateAvailable,
            currentVersion = currentVersion,
            latestVersion = highestVersionStr,
            releaseNotes = bestNotes,
            apkDownloadUrl = bestApkUrl,
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
}
