package org.mulletaflix.feature.player

/** Track choices that must survive a Media3 source/track rebuild. */
internal data class TrackRecoverySelection(
    val audioStreamIndex: Int?,
    val subtitleStreamIndex: Int?,
    val subtitlesDisabled: Boolean,
)

internal fun trackRecoverySelection(
    audioStreamIndex: Int?,
    subtitleStreamIndex: Int?,
    subtitlesDisabled: Boolean,
): TrackRecoverySelection = TrackRecoverySelection(
    audioStreamIndex = audioStreamIndex,
    subtitleStreamIndex = subtitleStreamIndex,
    subtitlesDisabled = subtitlesDisabled || subtitleStreamIndex == null,
)

/** Keep a recovery attempt alive while server metadata still advertises the target. */
internal fun shouldRetryTrackSelection(
    serverStreamIndex: Int,
    advertisedStreamIndices: Collection<Int>,
    selected: Boolean,
): Boolean = !selected && serverStreamIndex in advertisedStreamIndices
