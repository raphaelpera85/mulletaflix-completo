package org.mulletaflix.core.common.session

import java.net.URI
import java.security.MessageDigest

/** Immutable server and authentication identity captured when feedback submission starts. */
data class FeedbackRequestSession(
    val serverUrl: String,
    val accessToken: String,
    val userId: String,
    val deviceId: String,
    val serverId: String? = null,
) {
    override fun toString(): String =
        "FeedbackRequestSession(serverUrl=$serverUrl, accessToken=[REDACTED], userId=$userId, deviceId=$deviceId)"
}

/** Stable, credential-free scope for deferred reports across public/LAN server addresses. */
fun FeedbackRequestSession.playbackIssueScopeHash(): String {
    val serverIdentity = serverId?.trim()?.takeIf(String::isNotEmpty)?.let { "id:${it.lowercase()}" }
        ?: "url:${normalizeServerUrl(serverUrl)}"
    val source = "mulletaflix-playback-issue-v1|$serverIdentity|user:${userId.trim()}"
    return MessageDigest.getInstance("SHA-256")
        .digest(source.toByteArray(Charsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
}

private fun normalizeServerUrl(value: String): String = runCatching {
    val uri = URI(value.trim()).normalize()
    val scheme = uri.scheme?.lowercase().orEmpty()
    val host = uri.host?.lowercase().orEmpty()
    val port = uri.port.takeIf { it != -1 && !((scheme == "http" && it == 80) || (scheme == "https" && it == 443)) }
    val path = uri.path.orEmpty().trimEnd('/')
    "$scheme://$host${port?.let { ":$it" }.orEmpty()}$path"
}.getOrDefault(value.trim().trimEnd('/').lowercase())
