package org.mulletaflix.feature.itemdetail

import java.net.URLEncoder
import java.net.URI
import org.mulletaflix.designsystem.media.PUBLIC_SERVER_URL
import org.mulletaflix.designsystem.media.isLocalServerUrl

/** Builds the server-backed detail link shared by Android and browser clients. */
internal fun buildItemShareUrl(serverUrl: String, itemId: String, serverId: String? = null): String? {
    val baseUrl = canonicalShareBaseUrl(serverUrl)
    val normalizedId = itemId.trim()
    if (baseUrl.isBlank() || normalizedId.isBlank()) return null

    val encodedId = URLEncoder.encode(normalizedId, Charsets.UTF_8.name())
    val encodedServerId = serverId
        ?.trim()
        ?.takeIf(String::isNotBlank)
        ?.let { URLEncoder.encode(it, Charsets.UTF_8.name()) }
    return buildString {
        append("$baseUrl/web/#/details?id=$encodedId")
        // Without the server id a recipient on a different server resolves
        // the id against their own library and opens the wrong item.
        if (encodedServerId != null) append("&serverId=$encodedServerId")
    }
}

/**
 * A link copied from a local session must still be useful outside that
 * network, so any address the recipient cannot reach — loopback, or a private
 * LAN range — is replaced by the official public endpoint. A genuinely public
 * URL is kept exactly as configured by the user.
 */
internal fun canonicalShareBaseUrl(serverUrl: String): String {
    val normalized = serverUrl.trim().trimEnd('/')
    if (normalized.isBlank()) return normalized
    return if (isLocalServerUrl(normalized)) {
        PUBLIC_SERVER_URL
    } else {
        normalized
    }
}

internal fun buildItemShareText(
    name: String,
    itemId: String,
    serverUrl: String,
    serverId: String? = null,
): String {
    val title = name.trim().ifBlank { "um título" }
    return buildString {
        append("Confira \"$title\" no MulletaFlix.")
        buildItemShareUrl(serverUrl, itemId, serverId)?.let {
            append('\n')
            append(it)
        }
    }
}
