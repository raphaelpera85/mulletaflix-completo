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
