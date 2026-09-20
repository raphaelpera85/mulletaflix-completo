package org.mulletaflix.feature.user

/** Formats connection status without inventing a latency measurement. */
internal fun formatServerConnectionStatus(latencyMs: Long?): String =
    latencyMs
        ?.takeIf { it >= 0L }
        ?.let { "Online • $it ms de latência" }
        ?: "Conectado"
