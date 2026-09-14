package org.mulletaflix.designsystem.media

import androidx.compose.runtime.compositionLocalOf

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
        url + if (url.contains('?')) "&api_key=$it" else "?api_key=$it"
    } ?: url
}
