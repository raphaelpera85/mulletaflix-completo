package org.mulletaflix.android.network

import kotlinx.coroutines.delay
import org.mulletaflix.feature.auth.ServerInfo

/**
 * A LAN endpoint is preferred only when it is a real, local, different endpoint.
 *
 * The locality requirement used to be missing here while the opposite direction
 * ([publicFallbackAfterLanLoss]) had it, so any host that answered the port-7359
 * discovery probe — or advertised `0.0.0.0` — could replace the saved base URL,
 * and every later request, `Authorization` bearer header included, went there.
 */
internal fun shouldSwitchToLan(currentUrl: String, discoveredUrl: String): Boolean =
    discoveredUrl.isNotBlank() &&
    isLocalServerUrl(discoveredUrl) &&
    isDialableServerUrl(discoveredUrl) &&
    comparableServerUrl(discoveredUrl) != comparableServerUrl(currentUrl)

/**
 * Discovery responses commonly include a trailing slash while DataStore keeps
 * a manually entered URL without one. Compare endpoint identities rather than
 * their presentation so LAN recovery does not oscillate between aliases.
 */
private fun comparableServerUrl(url: String): String =
    url.trim().trimEnd('/').lowercase()

/** A successful login is enough to trigger a LAN re-check. */
internal fun shouldScanAfterAuthentication(userId: String?): Boolean =
    !userId.isNullOrBlank()

/**
 * Chooses only an endpoint that can be associated with the authenticated
 * server, and that is a local, dialable address.
 *
 * A legacy session without a server id may still switch automatically when
 * exactly one local server answers; multiple answers are ambiguous and must not
 * silently select the first server on a shared LAN — and a responder on a public
 * address is never a LAN candidate.
 */
internal fun selectAuthenticatedLanServer(
    discovered: List<ServerInfo>,
    authenticatedServerId: String?,
): ServerInfo? {
    val candidates = discovered.filter { server ->
        isLocalServerUrl(server.url) && isDialableServerUrl(server.url)
    }
    if (candidates.isEmpty()) return null
    if (authenticatedServerId.isNullOrBlank()) {
        return candidates.singleOrNull()
    }
    return candidates.firstOrNull { server ->
        server.serverId?.equals(authenticatedServerId, ignoreCase = true) == true
    }
}

/**
 * Returns true only for hosts that are unambiguously local/private.
 *
 * The classification now lives in the shared design-system module so link
 * sharing applies exactly the same rule.
 */
internal fun isLocalServerUrl(url: String): Boolean =
    org.mulletaflix.designsystem.media.isLocalServerUrl(url)

/** `0.0.0.0` is a listen address, not something a client can dial. */
internal fun isDialableServerUrl(url: String): Boolean =
    org.mulletaflix.designsystem.media.isDialableServerUrl(url)

/** Switches back to the public server only after a previous LAN endpoint fails discovery. */
internal fun publicFallbackAfterLanLoss(
    currentUrl: String,
    publicUrl: String,
    consecutiveMisses: Int = 1,
    requiredMisses: Int = 2,
): String? = publicUrl.takeIf {
    consecutiveMisses >= requiredMisses.coerceAtLeast(1) &&
    isLocalServerUrl(currentUrl) && comparableServerUrl(it) != comparableServerUrl(currentUrl)
}

/** A cancelled discovery must never mutate the active session after a newer scan. */
internal fun isCurrentLanScan(
    scanGeneration: Long,
    latestGeneration: Long,
    isStarted: Boolean,
): Boolean = isStarted && scanGeneration == latestGeneration

/** Waits for a burst of network callbacks, then lets only its newest scan probe. */
internal suspend fun shouldRunDebouncedLanScan(
    scanGeneration: Long,
    latestGeneration: () -> Long,
    isStarted: () -> Boolean,
    debounceMs: Long,
): Boolean {
    delay(debounceMs.coerceAtLeast(0L))
    return isCurrentLanScan(scanGeneration, latestGeneration(), isStarted())
}
