package org.mulletaflix.android

import android.net.Uri
import java.net.URLDecoder
import java.net.URI
import java.nio.charset.StandardCharsets

private const val OFFICIAL_SERVER_HOST = "mulletaflix.duckdns.org"

/** Extracts a Jellyfin/MulletaFlix item id from links shared by the server or APK. */
internal fun extractMediaItemId(uri: Uri?): String? {
    return uri?.toString()?.let(::extractMediaItemId)
}

/** String overload keeps the parser deterministic in JVM tests and Android intent handling. */
internal fun extractMediaItemId(rawUri: String?): String? {
    rawUri ?: return null
    val parsed = runCatching { URI(rawUri) }.getOrNull() ?: return null

    val scheme = parsed.scheme?.lowercase()
    val isMulletaFlixScheme = scheme == "mulletaflix"
    val webPath = parsed.path
    val isOfficialWebLink = scheme in setOf("http", "https") &&
        parsed.host?.equals(OFFICIAL_SERVER_HOST, ignoreCase = true) == true &&
        (webPath == "/web" || webPath?.startsWith("/web/") == true)
    if (!isMulletaFlixScheme && !isOfficialWebLink) return null

    val directId = queryParameter(parsed.rawQuery, "id")
    val fragmentId = parsed.rawFragment
        ?.substringAfter('?', "")
        ?.let { queryParameter(it, "id") }
    val pathId = parsed.path
        ?.split('/')
        ?.filter(String::isNotBlank)
        ?.dropWhile { it.equals("details", ignoreCase = true) || it.equals("item", ignoreCase = true) }
        ?.firstOrNull()

    return listOf(directId, fragmentId, pathId)
        .firstOrNull { !it.isNullOrBlank() }
        ?.trim()
        ?.takeIf { it.length <= 128 }
}

private fun queryParameter(encodedQuery: String?, key: String): String? =
    encodedQuery
        ?.takeIf(String::isNotBlank)
        ?.split('&')
        ?.asSequence()
        ?.map { it.substringBefore('=') to it.substringAfter('=', missingDelimiterValue = "") }
        ?.firstOrNull { (name, _) -> decode(name) == key }
        ?.second
        ?.let(::decode)

private fun decode(value: String): String =
    runCatching { URLDecoder.decode(value, StandardCharsets.UTF_8.name()) }.getOrDefault(value)
