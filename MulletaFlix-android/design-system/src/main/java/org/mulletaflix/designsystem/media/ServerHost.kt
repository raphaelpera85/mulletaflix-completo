package org.mulletaflix.designsystem.media

import java.net.URI

/**
 * Hosts that resolve only inside a local network or on the device itself.
 *
 * Shared by two callers that must agree:
 *
 * - LAN recovery, which prefers a private endpoint while at home;
 * - link sharing, which must never hand out an address the recipient cannot
 *   reach.
 */
private val LOOPBACK_HOSTS = setOf("localhost", "127.0.0.1", "0.0.0.0", "::1")

/** The official public endpoint, used whenever a private address must be replaced. */
const val PUBLIC_SERVER_URL = "http://mulletaflix.duckdns.org:8096"

/** Returns true only for hosts that are unambiguously local/private. */
fun isLocalServerUrl(url: String): Boolean {
    val host = runCatching { URI(url.trim()).host?.lowercase() }.getOrNull() ?: return false
    if (host in LOOPBACK_HOSTS) return true
    return isPrivateIpv4(host)
}

/** Returns true only for the loopback addresses of this device. */
fun isLoopbackServerUrl(url: String): Boolean {
    val host = runCatching { URI(url.trim()).host?.lowercase() }.getOrNull() ?: return false
    return host in LOOPBACK_HOSTS
}

private fun isPrivateIpv4(host: String): Boolean {
    val octets = host.split('.')
    if (octets.size != 4 || octets.any { it.toIntOrNull() == null }) return false
    val first = octets[0].toInt()
    val second = octets[1].toInt()
    return first == 10 ||
        (first == 172 && second in 16..31) ||
        (first == 192 && second == 168) ||
        (first == 169 && second == 254)
}
