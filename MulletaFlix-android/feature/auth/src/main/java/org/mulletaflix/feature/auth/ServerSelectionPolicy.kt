package org.mulletaflix.feature.auth

/** Chooses a discovered LAN endpoint before any saved/public endpoint. */
internal fun preferredServerUrl(
    discovered: List<ServerInfo>,
    saved: List<ServerInfo>,
    fallback: String?,
): String? = discovered.firstOrNull()?.url
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
