package org.mulletaflix.feature.player

import androidx.media3.common.Format
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.source.MediaSource

@UnstableApi
internal class PendingPlaybackVideoFormats(private val maxEntries: Int = 8) {
    private val pending = ArrayDeque<Pair<MediaSource.MediaPeriodId, Format>>()

    fun enqueue(periodId: MediaSource.MediaPeriodId, format: Format) {
        pending.removeAll { (queuedPeriod, _) ->
            queuedPeriod.equalsExceptNextAdGroupIndex(periodId)
        }
        pending.addLast(periodId to format)
        while (pending.size > maxEntries) pending.removeFirst()
    }

    fun takeFor(periodId: MediaSource.MediaPeriodId): Format? {
        val matching = pending.firstOrNull { (queuedPeriod, _) ->
            queuedPeriod.equalsExceptNextAdGroupIndex(periodId)
        } ?: return null
        pending.removeAll { (queuedPeriod, _) ->
            queuedPeriod.equalsExceptNextAdGroupIndex(periodId)
        }
        return matching.second
    }

    fun clear() = pending.clear()
}

/** Keeps queued or stale media-period events out of the active playback diagnostics. */
@UnstableApi
internal fun isPlaybackDiagnosticsEventForCurrentPeriod(
    eventPeriodId: MediaSource.MediaPeriodId?,
    currentPeriodId: MediaSource.MediaPeriodId?,
): Boolean = eventPeriodId != null && currentPeriodId != null &&
    eventPeriodId.equalsExceptNextAdGroupIndex(currentPeriodId)

@UnstableApi
internal fun takePlaybackVideoFormatForActiveTransition(
    pendingFormats: PendingPlaybackVideoFormats,
    transitionPeriodId: MediaSource.MediaPeriodId?,
    currentPeriodId: MediaSource.MediaPeriodId?,
): Format? {
    if (!isPlaybackDiagnosticsEventForCurrentPeriod(transitionPeriodId, currentPeriodId)) return null
    return pendingFormats.takeFor(transitionPeriodId!!)
}
