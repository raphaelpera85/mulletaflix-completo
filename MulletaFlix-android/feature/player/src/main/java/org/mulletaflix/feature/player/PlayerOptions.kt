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

internal data class VideoQualityConstraint(
    val maxWidth: Int,
    val maxHeight: Int,
    val maxBitrate: Int,
)

internal fun videoQualityConstraint(quality: String): VideoQualityConstraint = when (quality.uppercase()) {
    "4K" -> VideoQualityConstraint(3840, 2160, Int.MAX_VALUE)
    "1440P" -> VideoQualityConstraint(2560, 1440, 16_000_000)
    "1080P", "FULL HD" -> VideoQualityConstraint(1920, 1080, 10_000_000)
    "720P", "HD" -> VideoQualityConstraint(1280, 720, 6_000_000)
    "480P", "SD" -> VideoQualityConstraint(854, 480, 2_500_000)
    else -> VideoQualityConstraint(Int.MAX_VALUE, Int.MAX_VALUE, Int.MAX_VALUE)
}
