package org.mulletaflix.designsystem.media

import androidx.compose.runtime.compositionLocalOf
import java.net.URLEncoder
import java.nio.charset.StandardCharsets

/** Base address of the currently selected MulletaFlix server. */
val LocalMulletaFlixServerUrl = compositionLocalOf { "" }
val LocalMulletaFlixAccessToken = compositionLocalOf<String?> { null }

/** Converts API-relative image paths to authenticated-server-relative paths. */
fun resolveMediaUrl(baseUrl: String, path: String?, accessToken: String? = null): String? {
    if (path.isNullOrBlank()) return null
    if (path.startsWith("http://") || path.startsWith("https://")) return path
    val normalizedBase = baseUrl.trimEnd('/')
    if (normalizedBase.isBlank()) return null
    val url = "$normalizedBase/${path.trimStart('/')}"
    return accessToken?.takeIf { it.isNotBlank() }?.let {
        val encodedToken = URLEncoder.encode(it, StandardCharsets.UTF_8.name())
        url + if (url.contains('?')) "&api_key=$encodedToken" else "?api_key=$encodedToken"
    } ?: url
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
