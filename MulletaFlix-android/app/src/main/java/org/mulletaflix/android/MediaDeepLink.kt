package org.mulletaflix.android

import android.net.Uri
import java.net.URLDecoder
import java.net.URI
import java.nio.charset.StandardCharsets

private const val OFFICIAL_SERVER_HOST = "mulletaflix.duckdns.org"

/**
 * Segments that are part of the route rather than an item id.
 *
 * Without this, `http://mulletaflix.duckdns.org/web` parsed the literal
 * segment `web` as the item id and the app navigated to `detail/web`.
 */
private val RESERVED_PATH_SEGMENTS = setOf("web", "details", "item")

/** A media link shared by the server or the APK. */
internal data class MediaLink(
    val itemId: String,
    /** Server the link was generated for, when the link carries one. */
    val serverId: String? = null,
)

/** Extracts a Jellyfin/MulletaFlix item id from links shared by the server or APK. */
internal fun extractMediaItemId(uri: Uri?): String? = extractMediaLink(uri)?.itemId

/** String overload keeps the parser deterministic in JVM tests and Android intent handling. */
internal fun extractMediaItemId(rawUri: String?): String? = extractMediaLink(rawUri)?.itemId

/** Extracts the item id together with the server the link targets. */
internal fun extractMediaLink(uri: Uri?): MediaLink? = uri?.toString()?.let(::extractMediaLink)

internal fun extractMediaLink(rawUri: String?): MediaLink? {
    rawUri ?: return null
    val parsed = runCatching { URI(rawUri) }.getOrNull() ?: return null

    val scheme = parsed.scheme?.lowercase()
    val isMulletaFlixScheme = scheme == "mulletaflix"
    val webPath = parsed.path
    val isOfficialWebLink = scheme in setOf("http", "https") &&
        parsed.host?.equals(OFFICIAL_SERVER_HOST, ignoreCase = true) == true &&
        (webPath == "/web" || webPath?.startsWith("/web/") == true)
    if (!isMulletaFlixScheme && !isOfficialWebLink) return null

    val fragmentQuery = parsed.rawFragment?.substringAfter('?', "")?.takeIf(String::isNotBlank)
    val directId = queryParameter(parsed.rawQuery, "id") ?: queryParameter(fragmentQuery, "id")
    val pathId = parsed.path
        ?.split('/')
        ?.filter(String::isNotBlank)
        ?.filterNot { segment -> RESERVED_PATH_SEGMENTS.any { it.equals(segment, ignoreCase = true) } }
        ?.firstOrNull()

    val itemId = listOf(directId, pathId)
        .firstOrNull { !it.isNullOrBlank() }
        ?.trim()
        ?.takeIf { it.length <= 128 }
        ?: return null

    val serverId = (queryParameter(parsed.rawQuery, "serverId") ?: queryParameter(fragmentQuery, "serverId"))
        ?.trim()
        ?.takeIf { it.isNotBlank() && it.length <= 128 }

    return MediaLink(itemId = itemId, serverId = serverId)
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
