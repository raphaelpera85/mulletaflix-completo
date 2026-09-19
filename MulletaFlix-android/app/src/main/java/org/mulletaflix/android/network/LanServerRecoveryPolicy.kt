package org.mulletaflix.android.network

import java.net.URI
import org.mulletaflix.feature.auth.ServerInfo

/** A LAN endpoint is preferred only when it is a real, different endpoint. */
internal fun shouldSwitchToLan(currentUrl: String, discoveredUrl: String): Boolean =
    discoveredUrl.isNotBlank() &&
    !discoveredUrl.equals(currentUrl.trimEnd('/'), ignoreCase = true)

/** A successful login is enough to trigger a LAN re-check. */
internal fun shouldScanAfterAuthentication(userId: String?): Boolean =
    !userId.isNullOrBlank()

/**
 * Chooses only an endpoint that can be associated with the authenticated
 * server. A legacy session without a server id may still switch automatically
 * when exactly one server answers; multiple answers are ambiguous and must not
 * silently select the first server on a shared LAN.
 */
internal fun selectAuthenticatedLanServer(
    discovered: List<ServerInfo>,
    authenticatedServerId: String?,
): ServerInfo? {
    if (discovered.isEmpty()) return null
    if (authenticatedServerId.isNullOrBlank()) {
        return discovered.singleOrNull()
    }
    return discovered.firstOrNull { server ->
        server.serverId?.equals(authenticatedServerId, ignoreCase = true) == true
    }
}

/** Returns true only for hosts that are unambiguously local/private. */
internal fun isLocalServerUrl(url: String): Boolean {
    val host = runCatching { URI(url.trim()).host?.lowercase() }.getOrNull() ?: return false
    if (host == "localhost" || host == "127.0.0.1" || host == "::1") return true
    val octets = host.split('.')
    if (octets.size != 4 || octets.any { it.toIntOrNull() == null }) return false
    val first = octets[0].toInt()
    val second = octets[1].toInt()
    return first == 10 ||
        (first == 172 && second in 16..31) ||
        (first == 192 && second == 168) ||
        (first == 169 && second == 254)
}

/** Switches back to the public server only after a previous LAN endpoint fails discovery. */
internal fun publicFallbackAfterLanLoss(
    currentUrl: String,
    publicUrl: String,
): String? = publicUrl.takeIf {
    isLocalServerUrl(currentUrl) && !it.equals(currentUrl.trimEnd('/'), ignoreCase = true)
}
