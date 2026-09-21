package org.mulletaflix.core.api

import kotlinx.coroutines.flow.flowOf
import okhttp3.Interceptor
import okhttp3.Request
import okhttp3.Response
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.RecordedRequest
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/**
 * Contract test for the artwork client.
 *
 * The server throttles any request it cannot attribute to an authenticated
 * device to 30 requests per 10 s per IP, and a poster grid fires far more than
 * that. This test drives a real HTTP round trip through the exact client the app
 * gives to Coil and asserts what the server would see.
 */
class ArtworkClientIdentityTest {

    private lateinit var server: MockWebServer

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    private fun session(
        token: String? = "session-token",
        deviceId: String = "device-42",
        baseUrl: String = "http://127.0.0.1:8096",
    ) = object : SessionRepository {
        override fun getAccessToken() = flowOf(token)
        override fun getDeviceId() = flowOf(deviceId)
        override fun getBaseUrl() = flowOf(baseUrl)
        override fun getCurrentUserId() = flowOf("user-1")
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private fun client(sessionRepository: SessionRepository) = buildAuthenticatedImageClient(
        // The URL interceptor is not needed here: the request already targets
        // the mock server. A no-op keeps the chain identical in shape.
        serverUrlInterceptor = Interceptor { chain -> chain.proceed(chain.request()) },
        clientIdentityInterceptor = ClientIdentityInterceptor(sessionRepository),
    )

    private fun fetchArtwork(url: String, sessionRepository: SessionRepository): RecordedRequest {
        server.enqueue(MockResponse().setResponseCode(200).setBody("png"))
        client(sessionRepository).newCall(Request.Builder().url(url).build()).execute().close()
        return server.takeRequest()
    }

    @Test
    fun `artwork request carries the client identity and token`() {
        val recorded = fetchArtwork(
            url = "${server.url("/Items/movie-1/Images/Primary")}?api_key=session-token",
            sessionRepository = session(),
        )

        val authorization = recorded.getHeader("Authorization")
        assertNotNull("artwork must be authenticated", authorization)
        assertTrue(
            "expected the MulletaFlix client name in $authorization",
            authorization!!.contains("Client=\"MulletaFlix Android\""),
        )
        assertTrue(
            "expected the device id in $authorization",
            authorization.contains("DeviceId=\"device-42\""),
        )
        assertTrue(
            "expected the session token in $authorization",
            authorization.contains("Token=\"session-token\""),
        )
    }

    @Test
    fun `artwork request identifies the app in the user agent`() {
        val recorded = fetchArtwork(
            url = server.url("/Items/movie-1/Images/Primary").toString(),
            sessionRepository = session(),
        )

        assertEquals(
            "$MULLETAFLIX_USER_AGENT_PRODUCT/${BuildConfig.CLIENT_VERSION}",
            recorded.getHeader("User-Agent"),
        )
    }

    @Test
    fun `running the identity interceptor twice still yields one authorization value`() {
        // `addHeader` would leave two values on the request, and the server
        // parses Authorization as a single identity string.
        val sessionRepository = session()
        val identity = ClientIdentityInterceptor(sessionRepository)
        val chainedTwice = buildAuthenticatedImageClient(
            serverUrlInterceptor = Interceptor { chain -> chain.proceed(chain.request()) },
            clientIdentityInterceptor = Interceptor { chain -> identity.intercept(chain) },
        ).newBuilder()
            // A second copy of the same interceptor: the inner one sees the
            // request the outer one already stamped.
            .addInterceptor(identity)
            .build()

        server.enqueue(MockResponse().setResponseCode(200).setBody("png"))
        chainedTwice
            .newCall(Request.Builder().url(server.url("/Items/movie-1/Images/Primary")).build())
            .execute()
            .close()

        val recorded = server.takeRequest()
        assertEquals(
            "the Authorization header must have exactly one value",
            1,
            recorded.headers.values("Authorization").size,
        )
        assertEquals(
            "the User-Agent must have exactly one value",
            1,
            recorded.headers.values("User-Agent").size,
        )
    }

    @Test
    fun `a signed out session still identifies the client without leaking a token`() {
        val recorded = fetchArtwork(
            url = server.url("/Items/movie-1/Images/Primary").toString(),
            sessionRepository = session(token = null),
        )

        val authorization = recorded.getHeader("Authorization")
        assertNotNull(authorization)
        assertTrue(
            "the client must still be named when there is no token: $authorization",
            authorization!!.contains("Client=\"MulletaFlix Android\""),
        )
        assertTrue(
            "no token may be sent for a signed out session",
            !authorization.contains("Token="),
        )
    }
}
