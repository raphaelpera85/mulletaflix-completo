package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class OfflinePlaybackSessionTest {
    @Test
    fun `persisted session wins over a stale cached observer`() {
        assertEquals("persisted-user", resolveOfflinePlaybackUserId("cached-user", "persisted-user"))
    }

    @Test
    fun `cached session is used only while persisted state is unavailable`() {
        assertEquals("persisted-user", resolveOfflinePlaybackUserId(null, "persisted-user"))
        assertEquals("cached-user", resolveOfflinePlaybackUserId("cached-user", null))
        assertEquals("cached-user", resolveOfflinePlaybackUserId("cached-user", " "))
    }

    @Test
    fun `missing session prevents offline playback`() {
        assertNull(resolveOfflinePlaybackUserId(null, null))
        assertNull(resolveOfflinePlaybackUserId(" ", ""))
    }
}
