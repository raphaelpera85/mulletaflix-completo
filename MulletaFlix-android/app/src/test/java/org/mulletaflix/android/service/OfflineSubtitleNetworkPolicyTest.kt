package org.mulletaflix.android.service

import kotlinx.coroutines.flow.flowOf
import okhttp3.Interceptor
import okhttp3.Protocol
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import java.io.IOException
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.BuildConfig
import org.mulletaflix.core.api.MULLETAFLIX_USER_AGENT_PRODUCT
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.core.common.network.CleartextTrafficPolicy

class OfflineSubtitleNetworkPolicyTest {
    @Test
    fun `authenticated subtitle client refuses HTTP and HTTPS redirects`() {
        val client = offlineSubtitleHttpClient(Interceptor { chain -> chain.proceed(chain.request()) })

        assertFalse(client.followRedirects)
        assertFalse(client.followSslRedirects)
    }

    @Test
    fun `subtitle client blocks public HTTP before identity credentials are added`() {
        val session = subtitleSession(
            serverUrl = "http://203.0.113.20:8096",
            accessToken = "session-token",
            userId = "user-a",
            deviceId = "device-a",
            serverId = "server-a",
        )
        val client = offlineSubtitleHttpClient(ClientIdentityInterceptor(MutableSubtitleSessionRepository(session)))

        val failure = runCatching {
            client.newCall(authenticatedSubtitleRequest(
                "http://203.0.113.20:8096/Items/media/Subtitles/2/Stream?api_key=session-token",
                session,
            )).execute()
        }.exceptionOrNull()

        assertTrue("public HTTP must fail closed", failure is IOException)
        assertEquals(CleartextTrafficPolicy.BLOCKED_MESSAGE, failure?.message)
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

    @Test
    fun `subtitle client authenticates each request with its captured session`() {
        val oldSession = subtitleSession(
            serverUrl = "https://old.example",
            accessToken = "old-token",
            userId = "old-user",
            deviceId = "old-device",
            serverId = "old-server",
        )
        val activeSession = subtitleSession(
            serverUrl = "https://new.example",
            accessToken = "new-token",
            userId = "new-user",
            deviceId = "new-device",
            serverId = "new-server",
        )
        val repository = MutableSubtitleSessionRepository(activeSession)
        val observedRequests = mutableListOf<okhttp3.Request>()
        val client = offlineSubtitleHttpClient(ClientIdentityInterceptor(repository))
            .newBuilder()
            .addInterceptor { chain ->
                val request = chain.request()
                observedRequests += request
                Response.Builder()
                    .request(request)
                    .protocol(Protocol.HTTP_1_1)
                    .code(200)
                    .message("OK")
                    .body("subtitle".toResponseBody())
                    .build()
            }
            .build()

        val sessions = listOf(oldSession, activeSession)
        sessions.forEach { session ->
            val request = authenticatedSubtitleRequest(
                "${session.serverUrl}/Items/media/Subtitles/3/Stream",
                session,
            )
            client.newCall(request).execute().close()
        }

        assertEquals(2, observedRequests.size)
        sessions.zip(observedRequests).forEach { (session, request) ->
            assertEquals(
                "MediaBrowser Token=\"${session.accessToken}\", " +
                    "Client=\"MulletaFlix Android\", Device=\"Android\", " +
                    "DeviceId=\"${session.deviceId}\", Version=\"${BuildConfig.CLIENT_VERSION}\"",
                request.header("Authorization"),
            )
            assertEquals(
                "$MULLETAFLIX_USER_AGENT_PRODUCT/${BuildConfig.CLIENT_VERSION}",
                request.header("User-Agent"),
            )
            assertEquals(1, request.headers.values("Authorization").size)
            assertEquals(1, request.headers.values("User-Agent").size)
        }
    }

    private fun subtitleSession(
        serverUrl: String,
        accessToken: String,
        userId: String,
        deviceId: String,
        serverId: String,
    ) = FeedbackRequestSession(serverUrl, accessToken, userId, deviceId, serverId)

    private class MutableSubtitleSessionRepository(
        private val activeSession: FeedbackRequestSession,
    ) : SessionRepository {
        override fun getAccessToken() = flowOf(activeSession.accessToken)
        override fun getDeviceId() = flowOf(activeSession.deviceId)
        override fun getBaseUrl() = flowOf(activeSession.serverUrl)
        override fun getCurrentUserId() = flowOf(activeSession.userId)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
