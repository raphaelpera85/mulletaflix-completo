package org.mulletaflix.designsystem.media

import java.net.URI
import org.mulletaflix.core.common.network.CleartextTrafficPolicy

/**
 * Hosts that resolve only inside a local network or on the device itself.
 *
 * Shared by two callers that must agree:
 *
 * - LAN recovery, which prefers a private endpoint while at home;
 * - link sharing, which must never hand out an address the recipient cannot
 *   reach.
 */
private val LOOPBACK_HOSTS = setOf("localhost", "127.0.0.1", "0.0.0.0", "::", "::1")

/** The official public endpoint, used whenever a private address must be replaced. */
const val PUBLIC_SERVER_URL = "https://mulletaflix.duckdns.org"

/** Returns true only for hosts that are unambiguously local/private. */
fun isLocalServerUrl(url: String): Boolean {
    val host = normalizedHost(url) ?: return false
    if (host in LOOPBACK_HOSTS) return true
    return CleartextTrafficPolicy.isLocalNetworkHost(host)
}

/** Returns true only for the loopback addresses of this device. */
fun isLoopbackServerUrl(url: String): Boolean {
    val host = normalizedHost(url) ?: return false
    return host in LOOPBACK_HOSTS
}

/**
 * True when the URL names a host a client can open a connection to.
 *
 * `0.0.0.0` means "any local address" and is only ever valid as a *listen*
 * address: a server that advertises it in its discovery response cannot be
 * dialled, so it must never replace the saved endpoint — requests would fail, or
 * land somewhere unintended, with the Authorization header attached.
 *
 * It deliberately stays inside [isLocalServerUrl], because link sharing still has
 * to recognise it as an address that must be replaced by the public one.
 */
fun isDialableServerUrl(url: String): Boolean {
    val host = normalizedHost(url) ?: return false
    return host != "0.0.0.0" && host != "::"
}

private fun normalizedHost(url: String): String? = runCatching {
    URI(url.trim()).host?.trim()?.trim('[', ']')?.lowercase()
}.getOrNull()
