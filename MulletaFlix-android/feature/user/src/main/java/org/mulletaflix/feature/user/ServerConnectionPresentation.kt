package org.mulletaflix.feature.user

import java.net.URI

/** Formats connection status without inventing a latency measurement. */
internal fun formatServerConnectionStatus(latencyMs: Long?): String =
    latencyMs
        ?.takeIf { it >= 0L }
        ?.let { "Online • $it ms de latência" }
        ?: "Conectado"

internal fun activeServerUrl(primary: String, fallback: String): String =
    primary.trim().takeIf(String::isNotBlank) ?: fallback.trim()

internal enum class ServerConnectionMode {
    Lan,
    Internet,
    Remote,
}

/** Identifies the active route without exposing raw networking details in the UI. */
internal fun classifyServerConnection(url: String): ServerConnectionMode {
    val host = runCatching { URI(url.trim()).host?.lowercase() }.getOrNull()
        ?: return ServerConnectionMode.Remote
    return when {
        host == "localhost" || host == "127.0.0.1" || host == "::1" ||
            host.endsWith(".local") || isPrivateIpv4(host) -> ServerConnectionMode.Lan
        host == "mulletaflix.duckdns.org" -> ServerConnectionMode.Internet
        else -> ServerConnectionMode.Remote
    }
}

internal fun serverConnectionModeLabel(mode: ServerConnectionMode): String = when (mode) {
    ServerConnectionMode.Lan -> "Rede local (LAN)"
    ServerConnectionMode.Internet -> "Internet"
    ServerConnectionMode.Remote -> "Servidor remoto"
}

private fun isPrivateIpv4(host: String): Boolean {
    val octets = host.split('.')
    if (octets.size != 4 || octets.any { it.toIntOrNull() == null }) return false
    val first = octets[0].toInt()
    val second = octets[1].toInt()
    return first == 10 ||
        first == 192 && second == 168 ||
        first == 172 && second in 16..31 ||
        first == 169 && second == 254
}
