@file:androidx.annotation.OptIn(markerClass = [androidx.media3.common.util.UnstableApi::class])

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

/** Keeps persisted aspect-ratio values compatible with old or corrupted preferences. */
internal fun normalizeAspectRatioPreference(value: String?): VideoAspectRatio =
    when (value?.trim()?.uppercase()) {
        "ZOOM" -> VideoAspectRatio.ZOOM
        "FILL" -> VideoAspectRatio.FILL
        else -> VideoAspectRatio.FIT
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
)

/** Stable, user-facing text for sharing playback diagnostics with support. */
internal fun formatPlaybackStats(stats: PlaybackStats?, title: String? = null): String = buildString {
    appendLine("Dados técnicos da mídia")
    title?.takeIf(String::isNotBlank)?.let { appendLine("Título: $it") }
    appendLine("Método de Reprodução: ${stats?.playMethod ?: "Direct Play"}")
    stats?.resolution?.let { appendLine("Resolução: $it") }
    stats?.videoCodec?.let { appendLine("Codec de Vídeo: $it") }
    stats?.audioCodec?.let { appendLine("Codec de Áudio: $it") }
    stats?.bitrate?.let { appendLine("Taxa de Bits: $it") }
}.trimEnd()

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

/** Keeps the persisted quality preference valid when it comes from UI or old storage. */
internal fun normalizeQualityPreference(value: String?): String =
    when (value?.trim()?.uppercase()) {
        "AUTO", "AUTOMÁTICO", "AUTOMATICO" -> "Auto"
        "4K", "2160P", "2160" -> "4K"
        "1440P", "1440" -> "1440p"
        "1080P", "1080", "FULL HD" -> "1080p"
        "720P", "720", "HD" -> "720p"
        "480P", "480", "SD" -> "480p"
        else -> "Auto"
    }

/** Auto mode stays conservative on cellular/hotspot plans while manual choices remain authoritative. */
internal fun effectivePlaybackQuality(preference: String?, isMetered: Boolean): String {
    val normalized = normalizeQualityPreference(preference)
    return if (normalized == "Auto" && isMetered) "720p" else normalized
}

/** Makes the adaptive cap visible without changing the persisted Auto preference. */
internal fun qualityOptionLabel(quality: String, isMetered: Boolean): String =
    quality.trim().let { raw ->
        val normalized = normalizeQualityPreference(raw)
        val isAutoAlias = raw.equals("auto", ignoreCase = true) ||
            raw.equals("automático", ignoreCase = true) ||
            raw.equals("automatico", ignoreCase = true)
        when {
            isAutoAlias && isMetered -> "Auto (até 720p nesta rede)"
            isAutoAlias -> "Auto"
            normalized != "Auto" -> normalized
            else -> raw
        }
    }

/** Keeps the quality radio selection truthful when a title lacks the saved resolution. */
internal fun effectiveQualitySelection(
    preference: String?,
    availableQualities: List<String>,
): String {
    val normalized = normalizeQualityPreference(preference)
    return if (normalized == "Auto" || availableQualities.any { it.equals(normalized, ignoreCase = true) }) {
        normalized
    } else {
        "Auto"
    }
}

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
