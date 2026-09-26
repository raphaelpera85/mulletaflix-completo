@file:androidx.annotation.OptIn(markerClass = [androidx.media3.common.util.UnstableApi::class])

package org.mulletaflix.feature.player

import androidx.media3.ui.AspectRatioFrameLayout
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType
import org.mulletaflix.domain.model.QUALITY_AUTO
import org.mulletaflix.domain.model.normalizePlaybackQualityPreference
import org.mulletaflix.domain.model.qualityLabelForHeight as qualityLabelForHeightInDomain

/** Quality constraints can only be changed for the local Media3 player. */
internal fun qualityControlAvailable(isCasting: Boolean): Boolean = !isCasting

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
        .map(::qualityLabelForHeight)
        .distinct()
        .toList()

/**
 * Canonical label for a real video height.
 *
 * Delegates to the domain catalogue: the settings dialog names qualities with the same
 * function, and two copies of this mapping is how "360p" became unshowable there.
 */
internal fun qualityLabelForHeight(height: Int): String = qualityLabelForHeightInDomain(height)

internal fun qualityMenuOptions(qualities: List<String>): List<String> =
    (listOf(QUALITY_AUTO) + qualities).filter(String::isNotBlank).distinct()

/**
 * Keeps the persisted quality preference valid when it comes from UI or old storage.
 *
 * Delegates to the domain catalogue, which is also what the settings screen reads.
 */
internal fun normalizeQualityPreference(value: String?): String =
    normalizePlaybackQualityPreference(value)

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

/**
 * The quality the state should report as selected once [requested] is applied.
 *
 * Always a value the quality menu can show. Writing the stored preference raw
 * instead made the state claim a resolution the title did not offer, so
 * `qualityMenuOptions(availableQualities)` had no matching row and the control
 * appeared unset on every such title.
 */
internal fun appliedQualitySelection(
    requested: String?,
    availableQualities: List<String>,
): String = effectiveQualitySelection(requested, availableQualities)

internal data class VideoQualityConstraint(
    val maxWidth: Int,
    val maxHeight: Int,
    val maxBitrate: Int,
)

/**
 * One row of the resolution ladder: the label the user picks, and the cap it means.
 *
 * The five canonical rows are the only place these numbers are written down.
 */
internal data class QualityLadderStep(
    val preference: String,
    val maxWidth: Int,
    val maxHeight: Int,
    val maxBitrate: Int,
)

/** Canonical resolutions, largest first. */
internal val qualityLadder: List<QualityLadderStep> = listOf(
    QualityLadderStep("4K", 3840, 2160, Int.MAX_VALUE),
    QualityLadderStep("1440p", 2560, 1440, 16_000_000),
    QualityLadderStep("1080p", 1920, 1080, 10_000_000),
    QualityLadderStep("720p", 1280, 720, 6_000_000),
    QualityLadderStep("480p", 854, 480, 2_500_000),
)

internal fun videoQualityConstraint(quality: String): VideoQualityConstraint {
    val normalized = normalizeQualityPreference(quality)
    qualityLadder.firstOrNull { it.preference == normalized }?.let { step ->
        return VideoQualityConstraint(step.maxWidth, step.maxHeight, step.maxBitrate)
    }
    // Unlisted heights (360p, 576p, …) are real server tracks. Capping only the
    // height keeps the chosen resolution, and the tightest ladder row that still
    // contains it supplies a sane bitrate. Before, these fell through to "no
    // cap at all", so the player could serve a 4K stream to someone who had
    // explicitly asked for 360p.
    val height = normalized.removeSuffix("p").toIntOrNull()
        ?: return VideoQualityConstraint(Int.MAX_VALUE, Int.MAX_VALUE, Int.MAX_VALUE)
    val reference = qualityLadder.lastOrNull { it.maxHeight >= height }
    return VideoQualityConstraint(
        maxWidth = Int.MAX_VALUE,
        maxHeight = height,
        maxBitrate = reference?.maxBitrate ?: Int.MAX_VALUE,
    )
}
