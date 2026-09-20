package org.mulletaflix.feature.itemdetail

import java.net.URLEncoder
import java.net.URI

private const val PUBLIC_SERVER_URL = "http://mulletaflix.duckdns.org:8096"

/** Builds the server-backed detail link shared by Android and browser clients. */
internal fun buildItemShareUrl(serverUrl: String, itemId: String): String? {
    val baseUrl = canonicalShareBaseUrl(serverUrl)
    val normalizedId = itemId.trim()
    if (baseUrl.isBlank() || normalizedId.isBlank()) return null

    val encodedId = URLEncoder.encode(normalizedId, Charsets.UTF_8.name())
    return "$baseUrl/web/index.html#!/details?id=$encodedId"
}

/**
 * A link copied from a local emulator/development session must still be useful
 * outside that device. Only loopback hosts are replaced; real LAN and public
 * server URLs remain exactly as configured by the user.
 */
internal fun canonicalShareBaseUrl(serverUrl: String): String {
    val normalized = serverUrl.trim().trimEnd('/')
    if (normalized.isBlank()) return normalized
    val host = runCatching { URI(normalized).host?.lowercase() }.getOrNull()
    return if (host == "localhost" || host == "127.0.0.1" || host == "0.0.0.0" || host == "::1") {
        PUBLIC_SERVER_URL
    } else {
        normalized
    }
}

internal fun buildItemShareText(name: String, itemId: String, serverUrl: String): String {
    val title = name.trim().ifBlank { "um título" }
    return buildString {
        append("Confira \"$title\" no MulletaFlix.")
        buildItemShareUrl(serverUrl, itemId)?.let {
            append('\n')
            append(it)
        }
    }
}
