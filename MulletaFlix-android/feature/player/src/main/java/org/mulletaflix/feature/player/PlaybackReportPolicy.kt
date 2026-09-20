package org.mulletaflix.feature.player

/** Immutable values captured together before a delayed progress report. */
internal data class PlaybackProgressSnapshot(
    val generation: Long,
    val itemId: String?,
    val playSessionId: String?,
    val mediaSourceId: String?,
    val audioIndex: Int?,
    val subtitleIndex: Int?,
    val positionMs: Long,
    val isPaused: Boolean,
)

/**
 * Prevents a delayed server report from being attributed to a newer playback
 * load, even when the item id is reused for a new server session.
 */
internal fun isCurrentPlaybackReport(
    expectedGeneration: Long,
    currentGeneration: Long,
    expectedItemId: String?,
    currentItemId: String?,
    expectedPlaySessionId: String?,
    currentPlaySessionId: String?,
    expectedMediaSourceId: String?,
    currentMediaSourceId: String?,
): Boolean =
    expectedGeneration == currentGeneration &&
        expectedItemId == currentItemId &&
        expectedPlaySessionId == currentPlaySessionId &&
        expectedMediaSourceId == currentMediaSourceId
