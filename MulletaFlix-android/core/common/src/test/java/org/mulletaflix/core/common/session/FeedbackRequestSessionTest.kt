package org.mulletaflix.core.common.session

import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class FeedbackRequestSessionTest {
    @Test
    fun `toString redacts access token`() {
        val session = FeedbackRequestSession(
            serverUrl = "https://server.test",
            accessToken = "secret-token-value",
            userId = "user-1",
            deviceId = "device-1",
        )

        assertTrue(session.toString().contains("accessToken=[REDACTED]"))
        assertFalse(session.toString().contains("secret-token-value"))
    }

    @Test
    fun `same server identity shares scope between LAN and public URLs`() {
        val public = FeedbackRequestSession("http://mulletaflix.test:8096", "a", "user-1", "device-a", "server-id")
        val lan = FeedbackRequestSession("http://192.168.1.8:8096", "b", "user-1", "device-b", "server-id")

        assertEquals(public.playbackIssueScopeHash(), lan.playbackIssueScopeHash())
    }

    @Test
    fun `fallback scope normalizes URL but isolates accounts and servers`() {
        val base = FeedbackRequestSession("HTTPS://Server.test:443/", "token", "user-1", "device")

        assertEquals(
            base.playbackIssueScopeHash(),
            base.copy(serverUrl = "https://server.test", accessToken = "rotated", deviceId = "other").playbackIssueScopeHash(),
        )
        assertFalse(base.playbackIssueScopeHash() == base.copy(userId = "user-2").playbackIssueScopeHash())
        assertFalse(base.playbackIssueScopeHash() == base.copy(serverUrl = "https://other.test").playbackIssueScopeHash())
    }
}
