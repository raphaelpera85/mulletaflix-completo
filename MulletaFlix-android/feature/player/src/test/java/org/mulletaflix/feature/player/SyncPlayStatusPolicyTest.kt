package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest

@OptIn(ExperimentalCoroutinesApi::class)
class SyncPlayStatusPolicyTest {
    @Test
    fun `allows current event when group item state and connection still match`() {
        assertTrue(shouldSendSyncPlayStatus(
            eventGeneration = 3,
            currentGeneration = 3,
            eventGroupId = "room-a",
            currentGroupId = "room-a",
            eventPlaylistItemId = "item-a",
            currentPlaylistItemId = "item-a",
            isOfflinePlayback = false,
            networkIsOffline = false,
            realtimeConnected = true,
            eventIsBuffering = true,
            playerIsBuffering = true,
        ))
    }

    @Test
    fun `rejects stale state room item generation and offline events`() {
        val base = SyncPlayStatusTestContext()
        val invalidCases = listOf(
            base.copy(playerIsBuffering = false),
            base.copy(currentGroupId = "room-b"),
            base.copy(currentPlaylistItemId = "item-b"),
            base.copy(currentGeneration = 4),
            base.copy(isOfflinePlayback = true),
            base.copy(networkIsOffline = true),
            base.copy(realtimeConnected = false),
        )

        invalidCases.forEach { context -> assertFalse(context.toDecision()) }
    }

    @Test
    fun `reconnecting to same room creates a fresh deduplication key`() {
        val firstSession = syncPlayStatusDedupKey(8, "room-a", "item-a", false)
        val reconnectedSession = syncPlayStatusDedupKey(9, "room-a", "item-a", false)

        assertFalse(firstSession == reconnectedSession)
        assertEquals("8:room-a:item-a:false", firstSession)
    }

    @Test
    fun `processor preserves request order when network report is delayed`() = runTest {
        val firstReport = CompletableDeferred<Boolean>()
        var snapshot = statusSnapshot(playerIsBuffering = true)
        val sentStates = mutableListOf<Boolean>()
        val processor = SyncPlayStatusEventProcessor(
            scope = backgroundScope,
            currentSnapshot = { snapshot },
        ) { event ->
            sentStates += event.isBuffering
            if (sentStates.size == 1) firstReport.await() else true
        }

        assertTrue(processor.enqueue(statusEvent(isBuffering = true)))
        runCurrent()
        snapshot = snapshot.copy(playerIsBuffering = false)
        assertTrue(processor.enqueue(statusEvent(isBuffering = false)))
        assertEquals(listOf(true), sentStates)

        firstReport.complete(true)
        runCurrent()

        assertEquals(listOf(true, false), sentStates)
    }

    @Test
    fun `processor coalesces delayed queue to newest media state`() = runTest {
        val firstReport = CompletableDeferred<Boolean>()
        var snapshot = statusSnapshot(playerIsBuffering = true)
        val sentItems = mutableListOf<String>()
        val processor = SyncPlayStatusEventProcessor(
            scope = backgroundScope,
            currentSnapshot = { snapshot },
        ) { event ->
            sentItems += event.playlistItemId
            if (sentItems.size == 1) firstReport.await() else true
        }

        assertTrue(processor.enqueue(statusEvent(isBuffering = true)))
        runCurrent()
        snapshot = snapshot.copy(playlistItemId = "item-b", playerIsBuffering = false)
        assertTrue(processor.enqueue(statusEvent(itemId = "item-a", isBuffering = false)))
        snapshot = snapshot.copy(playerIsBuffering = true)
        assertTrue(processor.enqueue(statusEvent(itemId = "item-b", isBuffering = true)))

        firstReport.complete(true)
        runCurrent()

        assertEquals(listOf("item-a", "item-b"), sentItems)
    }

    @Test
    fun `processor drops pending report if network becomes offline`() = runTest {
        val firstReport = CompletableDeferred<Boolean>()
        var snapshot = statusSnapshot(playerIsBuffering = true)
        val sentStates = mutableListOf<Boolean>()
        val processor = SyncPlayStatusEventProcessor(
            scope = backgroundScope,
            currentSnapshot = { snapshot },
        ) { event ->
            sentStates += event.isBuffering
            if (sentStates.size == 1) firstReport.await() else true
        }

        assertTrue(processor.enqueue(statusEvent(isBuffering = true)))
        runCurrent()
        snapshot = snapshot.copy(playerIsBuffering = false)
        assertTrue(processor.enqueue(statusEvent(isBuffering = false)))
        snapshot = snapshot.copy(networkIsOffline = true)
        firstReport.complete(true)
        runCurrent()

        assertEquals(listOf(true), sentStates)
    }

    @Test
    fun `processor retries failed report and accepts same state after reconnection`() = runTest {
        var snapshot = statusSnapshot(playerIsBuffering = false)
        var shouldFail = true
        var sends = 0
        val processor = SyncPlayStatusEventProcessor(
            scope = backgroundScope,
            currentSnapshot = { snapshot },
        ) {
            sends++
            if (shouldFail) false else true
        }
        val firstSession = statusEvent(isBuffering = false)

        assertTrue(processor.enqueue(firstSession))
        runCurrent()
        shouldFail = false
        assertTrue(processor.enqueue(firstSession))
        runCurrent()
        snapshot = snapshot.copy(generation = 4)
        assertTrue(processor.enqueue(statusEvent(generation = 4, isBuffering = false)))
        runCurrent()

        assertEquals(3, sends)
    }

    @Test
    fun `leaving and rejoining same room reports same playback state again`() = runTest {
        val session = SyncPlayReportingSession()
        session.onConnected()
        var snapshot = statusSnapshot(playerIsBuffering = false).copy(
            generation = session.generation,
            realtimeConnected = session.isConnected,
        )
        var sends = 0
        val processor = SyncPlayStatusEventProcessor(
            scope = backgroundScope,
            currentSnapshot = { snapshot },
        ) { sends++; true }

        assertTrue(processor.enqueue(statusEvent(generation = session.generation, isBuffering = false)))
        runCurrent()
        session.onDisconnected()
        session.onConnected()
        snapshot = snapshot.copy(
            generation = session.generation,
            realtimeConnected = session.isConnected,
        )
        assertTrue(processor.enqueue(statusEvent(generation = session.generation, isBuffering = false)))
        runCurrent()

        assertEquals(2, sends)
    }

    @Test
    fun `processor rejects stale state before sending`() = runTest {
        var snapshot = statusSnapshot(playerIsBuffering = false)
        val sentStates = mutableListOf<Boolean>()
        val processor = SyncPlayStatusEventProcessor(
            scope = backgroundScope,
            currentSnapshot = { snapshot },
        ) { event -> sentStates += event.isBuffering; true }

        assertTrue(processor.enqueue(statusEvent(isBuffering = false)))
        snapshot = snapshot.copy(playerIsBuffering = true)
        assertTrue(processor.enqueue(statusEvent(isBuffering = true)))
        runCurrent()

        assertEquals(listOf(true), sentStates)
    }

    private fun statusEvent(
        generation: Long = 3,
        itemId: String = "item-a",
        isBuffering: Boolean,
    ) = SyncPlayStatusEvent(generation, "room-a", itemId, isBuffering)

    private fun statusSnapshot(
        playerIsBuffering: Boolean,
    ) = SyncPlayStatusSnapshot(
        generation = 3,
        groupId = "room-a",
        playlistItemId = "item-a",
        isOfflinePlayback = false,
        networkIsOffline = false,
        realtimeConnected = true,
        playerIsBuffering = playerIsBuffering,
    )

    private data class SyncPlayStatusTestContext(
        val eventGeneration: Long = 3,
        val currentGeneration: Long = 3,
        val eventGroupId: String = "room-a",
        val currentGroupId: String? = "room-a",
        val eventPlaylistItemId: String = "item-a",
        val currentPlaylistItemId: String? = "item-a",
        val isOfflinePlayback: Boolean = false,
        val networkIsOffline: Boolean = false,
        val realtimeConnected: Boolean = true,
        val eventIsBuffering: Boolean = true,
        val playerIsBuffering: Boolean = true,
    ) {
        fun toDecision() = shouldSendSyncPlayStatus(
            eventGeneration,
            currentGeneration,
            eventGroupId,
            currentGroupId,
            eventPlaylistItemId,
            currentPlaylistItemId,
            isOfflinePlayback,
            networkIsOffline,
            realtimeConnected,
            eventIsBuffering,
            playerIsBuffering,
        )
    }
}
