package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class OfflinePlaybackPositionTest {

    @Test
    fun `position key is stable and does not expose the media url`() {
        val uri = "https://server.example/Videos/movie/stream?api_key=secret"

        val key = offlinePlaybackPositionKey(uri)

        assertEquals(key, offlinePlaybackPositionKey(uri))
        assertNotEquals(uri, key)
        assertTrue(key.startsWith("position_"))
        assertEquals(73, key.length)
    }

    @Test
    fun `different media urls receive different position keys`() {
        assertNotEquals(
            offlinePlaybackPositionKey("https://server/Videos/movie-a/stream"),
            offlinePlaybackPositionKey("https://server/Videos/movie-b/stream"),
        )
    }

    @Test
    fun `remote position is scoped by user and item without exposing either`() {
        val key = remotePlaybackPositionKey("raphael", "movie-1")

        assertEquals(key, remotePlaybackPositionKey("raphael", "movie-1"))
        assertNotEquals(key, remotePlaybackPositionKey("other-user", "movie-1"))
        assertNotEquals(key, remotePlaybackPositionKey("raphael", "movie-2"))
        assertNotEquals("raphael", key)
        assertNotEquals("movie-1", key)
    }
}
