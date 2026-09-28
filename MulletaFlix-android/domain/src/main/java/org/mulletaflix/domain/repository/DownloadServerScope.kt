package org.mulletaflix.domain.repository

/**
 * Returns the identity used to isolate an offline download.
 *
 * The server id stays stable when the same server is reached over LAN or WAN.
 * The endpoint is only a fallback for servers that do not expose an id; in that
 * case changing the endpoint may create a second queue identity, but never
 * overwrites another server's download.
 */
fun downloadServerScopeId(serverId: String?, serverUrl: String): String? =
    serverId?.trim()?.takeIf(String::isNotEmpty)
        ?: serverUrl.trim().trimEnd('/').takeIf(String::isNotEmpty)
