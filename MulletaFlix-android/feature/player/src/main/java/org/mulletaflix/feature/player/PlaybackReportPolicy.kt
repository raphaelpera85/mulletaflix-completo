package org.mulletaflix.feature.player

/** Immutable values captured together before a delayed progress report. */
internal data class PlaybackProgressSnapshot(
    val generation: Long,
    val sessionGeneration: Long = 0L,
    val userId: String? = null,
    val itemId: String?,
    val playSessionId: String?,
    val mediaSourceId: String?,
    val audioIndex: Int?,
    val subtitleIndex: Int?,
    val positionMs: Long,
    val isPaused: Boolean,
)

/**
 * A new media load must close an already prepared remote session first. A
 * session that has not received a play-session or media-source identity yet
 * cannot be stopped reliably, so leave it for the normal load lifecycle.
 */
internal fun shouldReportRemotePlaybackBeforeLoad(
    currentItemId: String?,
    isOfflinePlayback: Boolean,
    playSessionId: String?,
    mediaSourceId: String?,
    stoppedReported: Boolean,
): Boolean =
    !isOfflinePlayback &&
        !stoppedReported &&
        !currentItemId.isNullOrBlank() &&
        (!playSessionId.isNullOrBlank() || !mediaSourceId.isNullOrBlank())

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
    expectedSessionGeneration: Long? = null,
    currentSessionGeneration: Long? = null,
    expectedUserId: String? = null,
    currentUserId: String? = null,
): Boolean =
    expectedGeneration == currentGeneration &&
        expectedItemId == currentItemId &&
        expectedPlaySessionId == currentPlaySessionId &&
        expectedMediaSourceId == currentMediaSourceId &&
        (expectedSessionGeneration == null || expectedSessionGeneration == currentSessionGeneration) &&
        (expectedUserId == null || expectedUserId == currentUserId)
