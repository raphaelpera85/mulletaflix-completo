package org.mulletaflix.feature.user

import java.net.URI
import org.mulletaflix.designsystem.media.isLocalServerUrl

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
        isLocalServerUrl(url) || host.endsWith(".local") -> ServerConnectionMode.Lan
        host == "mulletaflix.duckdns.org" -> ServerConnectionMode.Internet
        else -> ServerConnectionMode.Remote
    }
}

internal fun serverConnectionModeLabel(mode: ServerConnectionMode): String = when (mode) {
    ServerConnectionMode.Lan -> "Rede local (LAN)"
    ServerConnectionMode.Internet -> "Internet"
    ServerConnectionMode.Remote -> "Servidor remoto"
}
