package org.mulletaflix.feature.player

import androidx.media3.common.MimeTypes

/** Resolves the media type early so local and Cast players choose the right parser. */
internal fun playbackMimeType(container: String?, streamUrl: String): String? {
    val normalizedContainer = container?.trim()?.lowercase()?.removePrefix(".")
    val urlPath = streamUrl.substringBefore('?').lowercase()
    return when {
        normalizedContainer == "m3u8" || urlPath.endsWith(".m3u8") -> MimeTypes.APPLICATION_M3U8
        normalizedContainer == "mp4" || urlPath.endsWith(".mp4") -> MimeTypes.VIDEO_MP4
        normalizedContainer == "webm" || urlPath.endsWith(".webm") -> MimeTypes.VIDEO_WEBM
        normalizedContainer == "mkv" || normalizedContainer == "matroska" ||
            urlPath.endsWith(".mkv") -> MimeTypes.VIDEO_MATROSKA
        else -> null
    }
}
