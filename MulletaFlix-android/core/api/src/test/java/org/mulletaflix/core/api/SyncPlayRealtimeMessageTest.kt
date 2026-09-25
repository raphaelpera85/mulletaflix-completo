package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class SyncPlayRealtimeMessageTest {
    private val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
    private val adapter = moshi.adapter(SyncPlayWireEnvelope::class.java)

    @Test
    fun `parses pause command from server websocket envelope`() {
        val event = parseSyncPlayRealtimeEvent(
            adapter,
            """{"MessageType":"SyncPlayCommand","Data":{"GroupId":"g1","PlaylistItemId":"item-1","Command":"Pause","PositionTicks":123000,"When":"2026-09-24T12:00:00.000Z"}}""",
        )

        val command = event as SyncPlayRealtimeEvent.Command
        assertEquals("g1", command.groupId)
        assertEquals("item-1", command.playlistItemId)
        assertEquals("Pause", command.command)
        assertEquals(123000L, command.positionTicks)
        assertNotNull(command.whenEpochMs)
    }

    @Test
    fun `parses playing item from queue update`() {
        val event = parseSyncPlayRealtimeEvent(
            adapter,
            """{"MessageType":"SyncPlayPlayQueueUpdate","Data":{"GroupId":"g1","Data":{"Playlist":[{"ItemId":"media-1","PlaylistItemId":"playlist-1"}],"PlayingItemIndex":0,"StartPositionTicks":5000000,"IsPlaying":true}}}""",
        )

        assertEquals(
            SyncPlayRealtimeEvent.QueueUpdate("g1", "media-1", "playlist-1", 5000000L, true),
            event,
        )
    }

    @Test
    fun `parses group update and ignores unrelated websocket messages`() {
        val update = parseSyncPlayRealtimeEvent(
            adapter,
            """{"MessageType":"SyncPlayGroupUpdate","Data":{"GroupId":"g1","Type":"StateUpdate"}}""",
        )
        val unrelated = parseSyncPlayRealtimeEvent(
            adapter,
            """{"MessageType":"KeepAlive","Data":{"Message":"ok"}}""",
        )

        assertEquals(SyncPlayRealtimeEvent.GroupUpdate("g1", "StateUpdate"), update)
        assertNull(unrelated)
    }
}
