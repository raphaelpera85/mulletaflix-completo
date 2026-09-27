package org.mulletaflix.feature.downloads

import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.domain.repository.DownloadEntry

internal fun downloadArtworkModel(
    entry: DownloadEntry,
    serverUrl: String,
    accessToken: String?,
): String? = entry.offlineArtworkUri?.takeIf(String::isNotBlank)
    ?: resolveMediaUrl(serverUrl, entry.imageUrl, accessToken)
