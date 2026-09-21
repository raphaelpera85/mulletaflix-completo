package org.mulletaflix.designsystem.media

import androidx.compose.runtime.compositionLocalOf
import java.net.URI
import java.net.URLEncoder
import java.nio.charset.StandardCharsets

/** Base address of the currently selected MulletaFlix server. */
val LocalMulletaFlixServerUrl = compositionLocalOf { "" }
val LocalMulletaFlixAccessToken = compositionLocalOf<String?> { null }

/**
 * Identifier of the currently selected server.
 *
 * Shared links carry this id so a recipient on a different server can be told
 * which server the item belongs to instead of resolving the id against their
 * own library.
 */
val LocalMulletaFlixServerId = compositionLocalOf<String?> { null }

/** Converts API-relative image paths to authenticated-server-relative paths. */
fun resolveMediaUrl(baseUrl: String, path: String?, accessToken: String? = null): String? {
    if (path.isNullOrBlank()) return null
    if (path.startsWith("http://") || path.startsWith("https://")) {
        return path.withServerToken(baseUrl, accessToken)
            .also { logResolvedMediaUrl(it) }
    }
    val normalizedBase = baseUrl.trimEnd('/')
    if (normalizedBase.isBlank()) return null
    val url = "$normalizedBase/${path.trimStart('/')}"
    return accessToken?.takeIf { it.isNotBlank() }?.let {
        val encodedToken = URLEncoder.encode(it, StandardCharsets.UTF_8.name())
        url + if (url.contains('?')) "&api_key=$encodedToken" else "?api_key=$encodedToken"
    } ?: url
}

/**
 * Records the artwork URL actually handed to the image loader, with the token
 * redacted.
 *
 * A cover grid that reaches the server as *anonymous* is throttled by the
 * server's rate limiter and loads very slowly, and the only way to tell an
 * authenticated image URL from an anonymous one is to look at what was
 * requested. Debug builds only, and the token is never printed — logcat tag
 * `MulletaFlixImage`.
 */
private fun logResolvedMediaUrl(url: String) {
    if (!isDebuggableApp()) return
    android.util.Log.d(TAG_IMAGE_URL, "request ${redactToken(url)}")
}

internal const val TAG_IMAGE_URL = "MulletaFlixImage"

/** True only when the running application is a debuggable build. */
private fun isDebuggableApp(): Boolean = runCatching {
    val application = currentApplication() ?: return@runCatching false
    application.applicationInfo.flags and android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE != 0
}.getOrDefault(false)

private fun currentApplication(): android.app.Application? = runCatching {
    Class.forName("android.app.ActivityThread")
        .getMethod("currentApplication")
        .invoke(null) as? android.app.Application
}.getOrNull()

/** Replaces any token-bearing parameter value with a marker. */
fun redactToken(url: String): String = url
    .replace(Regex("(api_key=)[^&]*"), "$1<redacted>")
    .replace(Regex("(ApiKey=)[^&]*"), "$1<redacted>")
    .replace(Regex("(X-Emby-Token=)[^&]*"), "$1<redacted>")

private fun String.withServerToken(baseUrl: String, accessToken: String?): String {
    val token = accessToken?.takeIf { it.isNotBlank() } ?: return this
    val isSameOrigin = runCatching {
        val server = URI(baseUrl.trimEnd('/'))
        val media = URI(this)
        server.scheme.equals(media.scheme, ignoreCase = true) &&
            server.host.equals(media.host, ignoreCase = true) &&
            effectivePort(server) == effectivePort(media)
    }.getOrDefault(false)
    if (!isSameOrigin) return this

    val query = runCatching { URI(this).rawQuery.orEmpty() }.getOrDefault("")
    if (query.split('&').any { it.substringBefore('=').equals("api_key", ignoreCase = true) }) {
        return this
    }
    val encodedToken = URLEncoder.encode(token, StandardCharsets.UTF_8.name())
    return this + if (contains('?')) "&api_key=$encodedToken" else "?api_key=$encodedToken"
}

private fun effectivePort(uri: URI): Int = when {
    uri.port >= 0 -> uri.port
    uri.scheme.equals("https", ignoreCase = true) -> 443
    else -> 80
}

/**
 * Path of a server-side user profile image (avatar), or null when the server
 * has no image for that user.
 *
 * The `Tag` parameter is required: without it the image endpoint answers 200
 * with an empty body. The tag comes from `PrimaryImageTag` on the user payload.
 */
fun userAvatarPath(userId: String?, primaryImageTag: String?): String? {
    if (userId.isNullOrBlank() || primaryImageTag.isNullOrBlank()) return null
    return "Users/$userId/Images/Primary?tag=$primaryImageTag"
}
