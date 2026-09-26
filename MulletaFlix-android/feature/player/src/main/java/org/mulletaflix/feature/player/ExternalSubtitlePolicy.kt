package org.mulletaflix.feature.player

import android.net.Uri
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.media.retargetMediaUrl
import java.net.URI
import java.net.URLEncoder
import java.nio.charset.StandardCharsets

/** MIME types accepted by Media3's text subtitle parsers. */
internal fun externalSubtitleMimeType(codec: String?, deliveryUrl: String?): String? {
    val codecName = codec.orEmpty().substringBefore(',').trim().lowercase()
    val extension = runCatching { URI(deliveryUrl.orEmpty()).path.orEmpty() }
        .getOrDefault(deliveryUrl.orEmpty())
        .substringAfterLast('.', "")
        .lowercase()

    return when {
        codecName in setOf("srt", "subrip") || extension == "srt" -> "application/x-subrip"
        codecName in setOf("vtt", "webvtt") || extension == "vtt" -> "text/vtt"
        codecName in setOf("ass", "ssa") || extension in setOf("ass", "ssa") -> "text/x-ssa"
        codecName in setOf("ttml", "dfxp") || extension in setOf("ttml", "dfxp") -> "application/ttml+xml"
        else -> null
    }
}

/** Builds the exact fallback route already declared by [MulletaFlixApiService]. */
internal fun externalSubtitleStreamPath(
    itemId: String,
    streamIndex: Int,
    mediaSourceId: String?,
): String? {
    if (itemId.isBlank() || streamIndex < 0) return null
    val path = "Items/${encodePathSegment(itemId)}/Subtitles/$streamIndex/Stream"
    return mediaSourceId?.takeIf(String::isNotBlank)?.let {
        "$path?MediaSourceId=${encodeQueryValue(it)}"
    } ?: path
}

/**
 * Authenticates server-relative/same-origin subtitle URLs. Never sends the server
 * token to a different origin returned as a delivery URL.
 */
internal fun resolveExternalSubtitleUrl(
    baseUrl: String,
    deliveryUrl: String,
    accessToken: String?,
): String? {
    if (deliveryUrl.isBlank()) return null
    val normalizedUrl = if (deliveryUrl.startsWith("//")) {
        "${runCatching { URI(baseUrl).scheme }.getOrNull() ?: return null}:$deliveryUrl"
    } else {
        deliveryUrl
    }
    if (!normalizedUrl.startsWith("http://", ignoreCase = true) &&
        !normalizedUrl.startsWith("https://", ignoreCase = true)
    ) {
        val serverUrl = resolveMediaUrl(baseUrl, normalizedUrl, accessToken = null) ?: return null
        return retargetMediaUrl(serverUrl, baseUrl, accessToken)
    }
    return if (sameOrigin(baseUrl, normalizedUrl)) {
        retargetMediaUrl(normalizedUrl, baseUrl, accessToken)
    } else {
        val uri = runCatching { URI(normalizedUrl) }.getOrNull() ?: return null
        val sensitive = setOf("api_key", "x-emby-token", "access_token", "token")
        if (uri.rawUserInfo != null || uri.rawQuery.orEmpty().split('&').any { pair ->
                pair.substringBefore('=').lowercase() in sensitive
            }
        ) null else normalizedUrl
    }
}

private const val EXTERNAL_SUBTITLE_ID_PREFIX = "mullet-external:"

internal fun externalSubtitleServerIndex(formatId: String?): Int? =
    formatId?.takeIf { it.startsWith(EXTERNAL_SUBTITLE_ID_PREFIX) }
        ?.removePrefix(EXTERNAL_SUBTITLE_ID_PREFIX)?.toIntOrNull()?.takeIf { it >= 0 }

internal fun buildExternalSubtitleConfiguration(
    serverIndex: Int,
    subtitleUrl: String,
    mimeType: String,
    language: String?,
    label: String?,
    isDefault: Boolean,
    isForced: Boolean,
): MediaItem.SubtitleConfiguration {
    val selectionFlags = (if (isDefault) C.SELECTION_FLAG_DEFAULT else 0) or
        (if (isForced) C.SELECTION_FLAG_FORCED else 0)
    return MediaItem.SubtitleConfiguration.Builder(Uri.parse(subtitleUrl))
        .setId("$EXTERNAL_SUBTITLE_ID_PREFIX$serverIndex")
        .setMimeType(mimeType)
        .setLanguage(language)
        .setLabel(label)
        .setSelectionFlags(selectionFlags)
        .build()
}

private fun sameOrigin(first: String, second: String): Boolean = runCatching {
    val base = URI(first.trimEnd('/'))
    val candidate = URI(second)
    base.scheme.equals(candidate.scheme, ignoreCase = true) &&
        base.host.equals(candidate.host, ignoreCase = true) &&
        effectivePort(base) == effectivePort(candidate)
}.getOrDefault(false)

private fun effectivePort(uri: URI): Int = when {
    uri.port >= 0 -> uri.port
    uri.scheme.equals("https", ignoreCase = true) -> 443
    else -> 80
}

private fun encodePathSegment(value: String): String =
    URLEncoder.encode(value, StandardCharsets.UTF_8.name()).replace("+", "%20")

private fun encodeQueryValue(value: String): String =
    URLEncoder.encode(value, StandardCharsets.UTF_8.name())
