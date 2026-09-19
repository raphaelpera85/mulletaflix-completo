package org.mulletaflix.feature.itemdetail

import org.mulletaflix.domain.model.MediaSource

/** Prefers a ready-to-download Direct Play source and falls back to transcoding. */
internal fun preferredDownloadUrl(sources: List<MediaSource>): String? {
    return sources.firstNotNullOfOrNull { it.directStreamUrl?.trim()?.takeIf(String::isNotBlank) }
        ?: sources.firstNotNullOfOrNull { it.transcodeUrl?.trim()?.takeIf(String::isNotBlank) }
}
