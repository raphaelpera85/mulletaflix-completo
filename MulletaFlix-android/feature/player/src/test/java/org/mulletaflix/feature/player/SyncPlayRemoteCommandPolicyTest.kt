package org.mulletaflix.feature.player

import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.SyncPlayRealtimeEvent

class SyncPlayRemoteCommandPolicyTest {

    @Test
    fun `reconnecting state exposes a non blocking player message`() {
        assertEquals(
            "SyncPlay desconectado — tentando reconectar…",
            syncPlayConnectionMessage(SyncPlayConnectionState.RECONNECTING),
        )
        assertNull(syncPlayConnectionMessage(SyncPlayConnectionState.CONNECTED))
        assertNull(syncPlayConnectionMessage(SyncPlayConnectionState.NONE))
    }

    @Test
    fun `queue correction skips micro drift but fixes meaningful drift`() {
        assertNull(syncPlayQueueCorrectionPositionMs(10_000L, 10_500L))
        assertEquals(10_000L, syncPlayQueueCorrectionPositionMs(10_000L, 11_000L))
        assertEquals(0L, syncPlayQueueCorrectionPositionMs(-1L, 2_000L))
    }
    private val event = SyncPlayRealtimeEvent.Command(
        groupId = "group-1",
        playlistItemId = "playlist-1",
        command = "Pause",
        positionTicks = null,
    )

    @Test
    fun `accepts command for active room playlist item`() {
        assertTrue(
            shouldApplySyncPlayCommand(
                event = event,
                activeGroupId = "group-1",
                currentItemId = "media-1",
                currentPlaylistItemId = "playlist-1",
            ),
        )
    }

    @Test
    fun `rejects command from another room or media`() {
        assertFalse(shouldApplySyncPlayCommand(event, "group-2", "media-1", "playlist-1"))
        assertFalse(shouldApplySyncPlayCommand(event, "group-1", "media-2", "playlist-2"))
    }

    @Test
    fun `converts server ticks to player milliseconds`() {
        assertEquals(1234L, syncPlayPositionMs(12_345_678L))
    }

    @Test
    fun `pause and unpause use authoritative position but stop does not`() {
        assertEquals(1234L, syncPlayCommandPositionMs("Pause", 12_345_678L))
        assertEquals(1234L, syncPlayCommandPositionMs("Unpause", 12_345_678L))
        assertEquals(null, syncPlayCommandPositionMs("Stop", 12_345_678L))
    }
}
