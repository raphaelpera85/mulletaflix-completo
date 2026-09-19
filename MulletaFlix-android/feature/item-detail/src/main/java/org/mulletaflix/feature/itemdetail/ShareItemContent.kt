package org.mulletaflix.feature.itemdetail

import java.net.URLEncoder

/** Builds the server-backed detail link shared by Android and browser clients. */
internal fun buildItemShareUrl(serverUrl: String, itemId: String): String? {
    val baseUrl = serverUrl.trim().trimEnd('/')
    val normalizedId = itemId.trim()
    if (baseUrl.isBlank() || normalizedId.isBlank()) return null

    val encodedId = URLEncoder.encode(normalizedId, Charsets.UTF_8.name())
    return "$baseUrl/web/index.html#!/details?id=$encodedId"
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
