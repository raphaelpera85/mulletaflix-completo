package org.mulletaflix.feature.player

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch

internal fun syncPlayStatusDedupKey(
    generation: Long,
    groupId: String,
    playlistItemId: String,
    isBuffering: Boolean,
): String = "$generation:$groupId:$playlistItemId:$isBuffering"

internal fun shouldSendSyncPlayStatus(
    eventGeneration: Long,
    currentGeneration: Long,
    eventGroupId: String,
    currentGroupId: String?,
    eventPlaylistItemId: String,
    currentPlaylistItemId: String?,
    isOfflinePlayback: Boolean,
    networkIsOffline: Boolean,
    realtimeConnected: Boolean,
    eventIsBuffering: Boolean,
    playerIsBuffering: Boolean,
): Boolean = eventGeneration == currentGeneration &&
    eventGroupId == currentGroupId &&
    eventPlaylistItemId == currentPlaylistItemId &&
    !isOfflinePlayback &&
    !networkIsOffline &&
    realtimeConnected &&
    eventIsBuffering == playerIsBuffering

internal data class SyncPlayStatusEvent(
    val generation: Long,
    val groupId: String,
    val playlistItemId: String,
    val isBuffering: Boolean,
) {
    val key: String = syncPlayStatusDedupKey(generation, groupId, playlistItemId, isBuffering)
}

internal data class SyncPlayStatusSnapshot(
    val generation: Long,
    val groupId: String?,
    val playlistItemId: String?,
    val isOfflinePlayback: Boolean,
    val networkIsOffline: Boolean,
    val realtimeConnected: Boolean,
    val playerIsBuffering: Boolean,
) {
    fun allows(event: SyncPlayStatusEvent): Boolean = shouldSendSyncPlayStatus(
        eventGeneration = event.generation,
        currentGeneration = generation,
        eventGroupId = event.groupId,
        currentGroupId = groupId,
        eventPlaylistItemId = event.playlistItemId,
        currentPlaylistItemId = playlistItemId,
        isOfflinePlayback = isOfflinePlayback,
        networkIsOffline = networkIsOffline,
        realtimeConnected = realtimeConnected,
        eventIsBuffering = event.isBuffering,
        playerIsBuffering = playerIsBuffering,
    )
}

/** Serializes SyncPlay reports and retains only the newest pending state. */
internal class SyncPlayStatusEventProcessor(
    scope: CoroutineScope,
    private val currentSnapshot: () -> SyncPlayStatusSnapshot,
    private val report: suspend (SyncPlayStatusEvent) -> Boolean,
) {
    private val events = Channel<SyncPlayStatusEvent>(Channel.CONFLATED)
    private var lastQueuedKey: String? = null

    init {
        scope.launch {
            var lastSentKey: String? = null
            for (event in events) {
                if (event.key == lastSentKey) continue
                if (!currentSnapshot().allows(event)) {
                    clearPendingKey(event.key)
                    continue
                }
                if (report(event)) {
                    lastSentKey = event.key
                } else {
                    clearPendingKey(event.key)
                }
            }
        }
    }

    @Synchronized
    fun enqueue(event: SyncPlayStatusEvent): Boolean {
        if (event.key == lastQueuedKey) return false
        lastQueuedKey = event.key
        if (events.trySend(event).isSuccess) return true
        lastQueuedKey = null
        return false
    }

    @Synchronized
    private fun clearPendingKey(key: String) {
        if (lastQueuedKey == key) lastQueuedKey = null
    }
}
