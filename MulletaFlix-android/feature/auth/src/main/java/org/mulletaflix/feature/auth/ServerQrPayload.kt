package org.mulletaflix.feature.auth

import java.net.URI
import java.net.URLDecoder
import java.nio.charset.StandardCharsets

/** Extracts and validates a server URL encoded in a MulletaFlix QR payload. */
internal fun serverUrlFromQrPayload(rawPayload: String?): String? {
    val payload = rawPayload?.trim().orEmpty()
    if (payload.isBlank()) return null

    val candidate = runCatching {
        val uri = URI(payload)
        if (uri.scheme.equals("mulletaflix", ignoreCase = true)) {
            uri.rawQuery
                ?.split('&')
                ?.asSequence()
                ?.map { it.substringBefore('=') to it.substringAfter('=', "") }
                ?.firstOrNull { (key, _) -> decodeQrValue(key).equals("url", ignoreCase = true) }
                ?.second
                ?.let(::decodeQrValue)
        } else {
            payload
        }
    }.getOrNull() ?: return null

    return normalizeServerUrl(candidate)
}

private fun decodeQrValue(value: String): String =
    runCatching { URLDecoder.decode(value, StandardCharsets.UTF_8.name()) }.getOrDefault(value)
