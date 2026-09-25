package org.mulletaflix.feature.player

import org.mulletaflix.core.api.SyncPlayRealtimeEvent
import kotlin.math.abs

/**
 * Accepts a realtime command only when it belongs to the active room and the
 * current media. Playlist ids are authoritative; media ids are a safe fallback
 * for servers that send the media id in the command envelope.
 */
internal fun shouldApplySyncPlayCommand(
    event: SyncPlayRealtimeEvent.Command,
    activeGroupId: String?,
    currentItemId: String?,
    currentPlaylistItemId: String?,
): Boolean {
    if (activeGroupId.isNullOrBlank() || event.groupId != activeGroupId) return false
    if (event.playlistItemId.isBlank()) return false
    return event.playlistItemId == currentPlaylistItemId || event.playlistItemId == currentItemId
}

internal fun syncPlayPositionMs(positionTicks: Long?): Long? =
    positionTicks?.coerceAtLeast(0L)?.div(10_000L)

/** Pause/unpause carry the authoritative position; stop only changes playback state. */
internal fun syncPlayCommandPositionMs(command: String, positionTicks: Long?): Long? =
    when (command.lowercase()) {
        "pause", "unpause", "seek" -> syncPlayPositionMs(positionTicks)
        else -> null
    }

private const val SYNC_PLAY_QUEUE_DRIFT_TOLERANCE_MS = 750L

/**
 * Avoids a visible micro-seek when a queue heartbeat is already close to the
 * authoritative server position, while still correcting meaningful drift.
 */
internal fun syncPlayQueueCorrectionPositionMs(
    authoritativePositionMs: Long,
    currentPositionMs: Long,
    toleranceMs: Long = SYNC_PLAY_QUEUE_DRIFT_TOLERANCE_MS,
): Long? {
    val authoritative = authoritativePositionMs.coerceAtLeast(0L)
    val current = currentPositionMs.coerceAtLeast(0L)
    val tolerance = toleranceMs.coerceAtLeast(0L)
    return authoritative.takeIf { abs(it - current) > tolerance }
}
