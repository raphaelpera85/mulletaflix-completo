package org.mulletaflix.feature.auth

import java.net.URI

/** Canonicalizes a server base URL without losing an optional installation path. */
internal fun normalizeServerUrl(raw: String): String? = runCatching {
    val uri = URI(raw.trim())
    val scheme = uri.scheme?.lowercase() ?: return@runCatching null
    val host = uri.host?.lowercase() ?: return@runCatching null
    if (scheme !in setOf("http", "https") || uri.userInfo != null || uri.query != null || uri.fragment != null) {
        return@runCatching null
    }

    val normalizedHost = if (host.contains(':') && !host.startsWith('[')) "[$host]" else host
    val port = if (uri.port >= 0) ":${uri.port}" else ""
    val path = uri.path.trimEnd('/').takeUnless { it.isBlank() }.orEmpty()
    "$scheme://$normalizedHost$port$path"
}.getOrNull()
