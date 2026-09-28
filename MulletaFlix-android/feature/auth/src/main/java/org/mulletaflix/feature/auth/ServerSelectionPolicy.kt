package org.mulletaflix.feature.auth

internal data class ServerAutoConnectionCycle(
    val manuallyEdited: Boolean = false,
    val connectionStarted: Boolean = false,
    val requiredDiscoveryGeneration: Long = 0,
) {
    fun onManualAddressChanged(): ServerAutoConnectionCycle = copy(manuallyEdited = true)

    fun onDiscoveryRequested(generation: Long): ServerAutoConnectionCycle = copy(
        manuallyEdited = false,
        connectionStarted = false,
        requiredDiscoveryGeneration = generation,
    )

    fun onAutomaticConnectionStarted(): ServerAutoConnectionCycle = copy(connectionStarted = true)

    fun resetConnectionAttempt(): ServerAutoConnectionCycle = copy(connectionStarted = false)
}

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
): String? {
    discovered.firstOrNull { discoveredServer ->
        val discoveredId = discoveredServer.serverId?.takeIf(String::isNotBlank)
        discoveredId != null && saved.any { savedServer ->
            savedServer.serverId?.isNotBlank() == true && savedServer.serverId == discoveredId
        }
    }?.let { return it.url }

    // Once this installation knows server identities, an unmatched LAN
    // advertisement is not safe to auto-select. Keep the active/saved endpoint
    // instead; first-discovered fallback is only for first-time setup.
    if (saved.any { !it.serverId.isNullOrBlank() }) {
        return fallback ?: saved.firstOrNull()?.url
    }

    return discovered.firstOrNull()?.url
        ?: saved.firstOrNull()?.url
        ?: fallback
}

/**
 * Returns the endpoint that may be verified automatically during startup.
 *
 * The automatic path used to take `discoveredServers.firstOrNull()?.url`, which is
 * the one decision this file exists to avoid: on a network that advertises more
 * than one Jellyfin-compatible server, the first one to answer took over the app —
 * including over an install that was already logged in somewhere else. The
 * identity match that [preferredServerUrl] implements was only applied to the
 * *displayed* address, not to the endpoint the screen connects to by itself.
 *
 * When discovery returns nothing, the saved address still wins, so a first run
 * with no LAN server keeps verifying the public endpoint.
 */
internal fun automaticServerCandidate(
    state: AuthState,
    manuallyEdited: Boolean,
    connectionStarted: Boolean,
    requiredDiscoveryGeneration: Long = 0,
): String? = if (
    !manuallyEdited &&
    !connectionStarted &&
    !state.isDiscovering &&
    !state.isLocalNetworkPermissionRequired &&
    state.completedLocalDiscoveryGeneration >= requiredDiscoveryGeneration &&
    state.savedServersLoaded
) {
    if (state.discoveredServers.isEmpty()) {
        state.serverUrl
    } else {
        preferredServerUrl(
            discovered = state.discoveredServers,
            saved = state.savedServers,
            fallback = state.serverUrl,
        )
    }
} else {
    null
}

/** Selects a non-failed saved endpoint, then the public default. */
internal fun fallbackServerCandidate(state: AuthState, failedEndpoint: String): String =
    state.savedServers.firstOrNull { it.url != failedEndpoint }?.url
        ?: DEFAULT_MULLETAFLIX_SERVER_URL

/** Retry the saved/public endpoint after any discovered LAN endpoint fails. */
internal fun shouldTryFallbackAfterDiscoveryFailure(
    discovered: List<ServerInfo>,
    failedEndpoint: String,
): Boolean = discovered.any { it.url == failedEndpoint }
