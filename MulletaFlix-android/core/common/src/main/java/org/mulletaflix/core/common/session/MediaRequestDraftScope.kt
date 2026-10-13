package org.mulletaflix.core.common.session

import java.net.URI
import java.security.MessageDigest

/** Stable, credential-free scope for a local media-request draft. */
fun FeedbackRequestSession.mediaRequestDraftScopeHash(): String {
    val serverIdentity = serverId?.trim()?.takeIf(String::isNotEmpty)?.let { "id:${it.lowercase()}" }
        ?: "url:${normalizeDraftServerUrl(serverUrl)}"
    val source = "mulletaflix-media-request-draft-v1|$serverIdentity|user:${userId.trim()}"
    return MessageDigest.getInstance("SHA-256")
        .digest(source.toByteArray(Charsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
}

private fun normalizeDraftServerUrl(value: String): String = runCatching {
    val uri = URI(value.trim()).normalize()
    val scheme = uri.scheme?.lowercase().orEmpty()
    val host = uri.host?.lowercase().orEmpty()
    val port = uri.port.takeIf { it != -1 && !((scheme == "http" && it == 80) || (scheme == "https" && it == 443)) }
    val path = uri.path.orEmpty().trimEnd('/')
    "$scheme://$host${port?.let { ":$it" }.orEmpty()}$path"
}.getOrDefault(value.trim().trimEnd('/').lowercase())
