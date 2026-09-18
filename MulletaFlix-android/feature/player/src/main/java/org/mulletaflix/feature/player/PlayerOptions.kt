package org.mulletaflix.feature.player

import androidx.media3.ui.AspectRatioFrameLayout
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

/**
 * Aspect ratio display mode for the video player.
 */
enum class VideoAspectRatio(val title: String, val resizeMode: Int) {
    FIT("Ajustar (Original)", AspectRatioFrameLayout.RESIZE_MODE_FIT),
    ZOOM("Preencher / Zoom", AspectRatioFrameLayout.RESIZE_MODE_ZOOM),
    FILL("Esticar", AspectRatioFrameLayout.RESIZE_MODE_FILL),
}

/**
 * Technical playback statistics ("Stats for nerds") for media inspection.
 */
data class PlaybackStats(
    val videoCodec: String? = null,
    val audioCodec: String? = null,
    val resolution: String? = null,
    val bitrate: String? = null,
    val playMethod: String = "Direct Play",
    val framerate: Float? = null,
)

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

internal fun qualityMenuOptions(qualities: List<String>): List<String> =
    (listOf("Auto") + qualities).filter(String::isNotBlank).distinct()

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
