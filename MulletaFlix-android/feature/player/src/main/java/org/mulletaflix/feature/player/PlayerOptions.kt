package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

/** Returns stable, user-facing quality choices from the actual video tracks. */
internal fun qualityOptions(mediaStreams: List<MediaStream>): List<String> =
    mediaStreams
        .asSequence()
        .filter { it.type == MediaStreamType.Video && (it.height ?: 0) > 0 }
        .mapNotNull { it.height }
        .distinct()
        .sortedDescending()
        .map { height ->
            when {
                height >= 2160 -> "4K"
                height >= 1440 -> "1440p"
                height >= 1080 -> "1080p"
                height >= 720 -> "720p"
                height >= 480 -> "480p"
                else -> "${height}p"
            }
        }
        .distinct()
        .toList()
