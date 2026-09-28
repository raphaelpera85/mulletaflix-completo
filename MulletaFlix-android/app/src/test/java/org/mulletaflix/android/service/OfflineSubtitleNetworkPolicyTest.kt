package org.mulletaflix.android.service

import okhttp3.Interceptor
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test
import org.mulletaflix.core.common.session.FeedbackRequestSession

class OfflineSubtitleNetworkPolicyTest {
    @Test
    fun `authenticated subtitle client refuses HTTP and HTTPS redirects`() {
        val client = offlineSubtitleHttpClient(Interceptor { chain -> chain.proceed(chain.request()) })

        assertFalse(client.followRedirects)
        assertFalse(client.followSslRedirects)
    }

    @Test
    fun `subtitle request carries the same atomic session used to resolve its URL`() {
        val session = FeedbackRequestSession(
            serverUrl = "https://media.example",
            accessToken = "session-token",
            userId = "user-a",
            deviceId = "device-a",
            serverId = "server-a",
        )

        val request = authenticatedSubtitleRequest(
            "https://media.example/Items/media-a/Subtitles/2/Stream?api_key=session-token",
            session,
        )

        assertEquals(session, request.tag(FeedbackRequestSession::class.java))
    }
}
