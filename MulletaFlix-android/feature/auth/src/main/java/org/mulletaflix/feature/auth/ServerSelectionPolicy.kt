package org.mulletaflix.feature.auth

/**
 * Chooses the saved server's LAN endpoint before any other discovered server.
 *
 * A network can contain more than one Jellyfin-compatible server. When the
 * discovery payload includes an identity already known by the app, matching
 * it prevents an unrelated server from silently taking over startup. If the
 * identity is unavailable, the first discovered LAN endpoint remains the
 * compatible fallback for first-time setup.
 */
internal fun preferredServerUrl(
    discovered: List<ServerInfo>,
    saved: List<ServerInfo>,
    fallback: String?,
): String? = discovered.firstOrNull { discoveredServer ->
    discoveredServer.serverId != null && saved.any { savedServer ->
        savedServer.serverId == discoveredServer.serverId
    }
}?.url
    ?: discovered.firstOrNull()?.url
    ?: saved.firstOrNull()?.url
    ?: fallback

/** Returns the endpoint that may be verified automatically during startup. */
internal fun automaticServerCandidate(
    state: AuthState,
    manuallyEdited: Boolean,
    connectionStarted: Boolean,
): String? = if (!manuallyEdited && !connectionStarted && !state.isDiscovering) {
    state.discoveredServers.firstOrNull()?.url ?: state.serverUrl
} else {
    null
}

/** Selects a non-failed saved endpoint, then the public default. */
internal fun fallbackServerCandidate(state: AuthState, failedEndpoint: String): String =
    state.savedServers.firstOrNull { it.url != failedEndpoint }?.url
        ?: DEFAULT_MULLETAFLIX_SERVER_URL
