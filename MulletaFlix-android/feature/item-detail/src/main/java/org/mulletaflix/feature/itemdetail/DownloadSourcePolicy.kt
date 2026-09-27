package org.mulletaflix.feature.itemdetail

import org.mulletaflix.domain.model.MediaSource
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import java.net.URI

internal data class PreferredDownloadSource(
    val source: MediaSource,
    val url: String,
)

internal fun preferredDownloadSource(sources: List<MediaSource>): PreferredDownloadSource? {
    val direct = sources.firstNotNullOfOrNull { source ->
        source.directStreamUrl?.trim()?.takeIf(String::isNotBlank)?.let { PreferredDownloadSource(source, it) }
    }
    if (direct != null) return direct
    return sources.firstNotNullOfOrNull { source ->
        source.transcodeUrl?.trim()?.takeIf(String::isNotBlank)?.let { PreferredDownloadSource(source, it) }
    }
}

/** Prefers a ready-to-download Direct Play source and falls back to transcoding. */
internal fun preferredDownloadUrl(sources: List<MediaSource>): String? {
    return preferredDownloadSource(sources)?.url
}

/** Returns safe server-side fallback metadata, never delivery URLs or credentials. */
internal fun downloadableExternalSubtitles(source: MediaSource): List<DownloadSubtitleMetadata> =
    source.mediaStreams.asSequence()
        .filter { it.type == MediaStreamType.Subtitle && it.isExternal }
        .mapNotNull { stream ->
            val mimeType = downloadSubtitleMimeType(stream) ?: return@mapNotNull null
            stream.takeIf { it.index >= 0 }?.let {
                DownloadSubtitleMetadata(
                    streamIndex = it.index,
                    mimeType = mimeType,
                    language = it.language ?: it.displayLanguage,
                    label = it.displayTitle ?: it.title ?: it.displayLanguage ?: it.language,
                    isDefault = it.isDefault,
                    isForced = it.isForced,
                )
            }
        }
        .distinctBy { it.streamIndex }
        .sortedWith(compareByDescending<DownloadSubtitleMetadata> { it.isDefault }.thenBy { it.streamIndex })
        .take(MAX_OFFLINE_SUBTITLE_STREAMS)
        .toList()

private fun downloadSubtitleMimeType(stream: MediaStream): String? {
    val codec = stream.codec.orEmpty().substringBefore(',').trim().lowercase()
    val extension = runCatching { URI(stream.deliveryUrl.orEmpty()).path.orEmpty() }
        .getOrDefault("")
        .substringAfterLast('.', "")
        .lowercase()
    return when {
        codec in setOf("srt", "subrip") || extension == "srt" -> "application/x-subrip"
        codec in setOf("vtt", "webvtt") || extension == "vtt" -> "text/vtt"
        codec in setOf("ass", "ssa") || extension in setOf("ass", "ssa") -> "text/x-ssa"
        codec in setOf("ttml", "dfxp") || extension in setOf("ttml", "dfxp") -> "application/ttml+xml"
        else -> null
    }
}

private const val MAX_OFFLINE_SUBTITLE_STREAMS = 16
