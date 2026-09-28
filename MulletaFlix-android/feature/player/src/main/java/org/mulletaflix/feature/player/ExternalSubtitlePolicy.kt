package org.mulletaflix.feature.player

import android.net.Uri
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.media.retargetMediaUrl
import java.net.URI
import java.net.URLDecoder
import java.net.URLEncoder
import java.nio.charset.StandardCharsets

/** MIME types accepted by Media3's text subtitle parsers. */
internal fun externalSubtitleMimeType(codec: String?, deliveryUrl: String?): String? {
    val codecName = codec.orEmpty().substringBefore(',').trim().lowercase()
    val extension = runCatching { URI(deliveryUrl.orEmpty()).path.orEmpty() }
        .getOrDefault(deliveryUrl.orEmpty())
        .substringAfterLast('.', "")
        .lowercase()

    // Jellyfin may expose an SRT source through a server-converted Stream.vtt
    // delivery URL. The URL describes the bytes the player receives; the codec
    // describes the source stream and must not override that response format.
    val deliveryMimeType = when (extension) {
        "srt", "subrip" -> "application/x-subrip"
        "vtt", "webvtt" -> "text/vtt"
        "ass", "ssa" -> "text/x-ssa"
        "ttml", "dfxp" -> "application/ttml+xml"
        else -> null
    }
    return deliveryMimeType ?: when (codecName) {
        "srt", "subrip" -> "application/x-subrip"
        "vtt", "webvtt" -> "text/vtt"
        "ass", "ssa" -> "text/x-ssa"
        "ttml", "dfxp" -> "application/ttml+xml"
        else -> null
    }
}

/** Text subtitle formats accepted by the default Cast receiver. */
internal fun castSubtitleContentType(media3MimeType: String?): String? = when (media3MimeType?.lowercase()) {
    "text/vtt" -> "text/vtt"
    "application/ttml+xml" -> "application/ttml+xml"
    else -> null
}

internal fun castSubtitleTrackId(serverIndex: Int): Long? =
    serverIndex.takeIf { it >= 0 }?.let { CAST_SUBTITLE_TRACK_ID_PREFIX + it }

internal fun castSubtitleServerIndicesByUrl(
    configurations: Map<Int, MediaItem.SubtitleConfiguration>,
): Map<String, Int> = configurations.mapNotNull { (serverIndex, configuration) ->
    if (castSubtitleContentType(configuration.mimeType) == null || castSubtitleTrackId(serverIndex) == null) {
        return@mapNotNull null
    }
    configuration.uri.toString()
        .takeIf(String::isNotBlank)
        ?.let { url -> url to serverIndex }
}.groupBy({ it.first }, { it.second })
    .filterValues { indices -> indices.size == 1 }
    .mapValues { (_, indices) -> indices.single() }

private const val CAST_SUBTITLE_TRACK_ID_PREFIX = 0x4D554C4C00000000L

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
        if (uri.rawUserInfo != null || hasCredentialQueryParameter(uri.rawQuery)) null else normalizedUrl
    }
}

private fun hasCredentialQueryParameter(rawQuery: String?): Boolean = rawQuery.orEmpty()
    .split('&')
    .asSequence()
    .map { it.substringBefore('=') }
    .map { rawName -> runCatching { URLDecoder.decode(rawName, StandardCharsets.UTF_8.name()) }.getOrDefault(rawName) }
    .map { name -> name.lowercase().filter(Char::isLetterOrDigit) }
    .any { name ->
        name in CREDENTIAL_PARAMETER_NAMES ||
            listOf("token", "secret", "credential", "password", "authorization").any(name::contains)
    }

private val CREDENTIAL_PARAMETER_NAMES = setOf(
    "apikey", "accesskey", "auth", "authkey", "key", "session", "sessionid", "ticket",
)

private const val EXTERNAL_SUBTITLE_ID_PREFIX = "mullet-external:"

internal fun externalSubtitleServerIndex(formatId: String?): Int? =
    formatId?.takeIf { it.startsWith(EXTERNAL_SUBTITLE_ID_PREFIX) }
        ?.removePrefix(EXTERNAL_SUBTITLE_ID_PREFIX)?.toIntOrNull()?.takeIf { it >= 0 }

internal fun externalSubtitleServerIndex(
    formatId: String?,
    castSubtitleServerIndicesByUrl: Map<String, Int>,
): Int? = castSubtitleServerIndicesByUrl[formatId] ?: externalSubtitleServerIndex(formatId)

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
