package org.mulletaflix.core.api

import java.io.IOException
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.flow.flowOf
import org.mulletaflix.core.api.di.NetworkModule
import okhttp3.Interceptor
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class CleartextRequestProtectionTest {
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

    @Test
    fun `Retrofit client blocks a saved public HTTP server before reading session credentials`() {
        val sessions = CountingSessionRepository(baseUrl = "http://203.0.113.9:8096")
        val client = NetworkModule.provideOkHttpClient(
            clientIdentityInterceptor = ClientIdentityInterceptor(sessions),
            apiRetryInterceptor = ApiRetryInterceptor(),
            serverUrlInterceptor = ServerUrlInterceptor(sessions),
        )

        val failure = runCatching {
            client.newCall(
                Request.Builder()
                    .url("https://placeholder.example/Users")
                    .post(ByteArray(0).toRequestBody())
                    .build(),
            ).execute()
        }.exceptionOrNull()

        assertTrue("public HTTP must fail before a network request", failure is IOException)
        assertEquals("the token must not be read", 0, sessions.tokenReads.get())
        assertEquals("no request may reach the network", 0, server.requestCount)
    }

    @Test
    fun `Retrofit redirect to public HTTP is blocked after the local request`() {
        server.enqueue(
            MockResponse()
                .setResponseCode(302)
                .addHeader("Location", "http://203.0.113.10:8096/external?api_key=session-token"),
        )
        val sessions = CountingSessionRepository(baseUrl = server.url("/").toString())
        val client = NetworkModule.provideOkHttpClient(
            clientIdentityInterceptor = ClientIdentityInterceptor(sessions),
            apiRetryInterceptor = ApiRetryInterceptor(),
            serverUrlInterceptor = ServerUrlInterceptor(sessions),
        )

        val failure = runCatching {
            client.newCall(
                Request.Builder()
                    .url("https://placeholder.example/Users")
                    .post(ByteArray(0).toRequestBody())
                    .build(),
            ).execute()
        }.exceptionOrNull()

        assertTrue("the redirect destination must be denied", failure is IOException)
        assertEquals("only the initial LAN request may reach the server", 1, server.requestCount)
        assertTrue(server.takeRequest().getHeader("Authorization").orEmpty().contains("Token=\"session-token\""))
    }

    @Test
    fun `Retrofit follows redirects that remain on the configured LAN server`() {
        server.enqueue(MockResponse().setResponseCode(302).addHeader("Location", "/Users/next"))
        server.enqueue(MockResponse().setBody("{}"))
        val sessions = CountingSessionRepository(baseUrl = server.url("/").toString())
        val client = NetworkModule.provideOkHttpClient(
            clientIdentityInterceptor = ClientIdentityInterceptor(sessions),
            apiRetryInterceptor = ApiRetryInterceptor(),
            serverUrlInterceptor = ServerUrlInterceptor(sessions),
        )

        val response = client.newCall(Request.Builder().url("https://placeholder.example/Users").build()).execute()

        assertEquals(200, response.code)
        response.close()
        assertEquals(2, server.requestCount)
        assertEquals("/Users", server.takeRequest().path)
        assertEquals("/Users/next", server.takeRequest().path)
    }

    @Test
    fun `cross origin local redirect removes credentials but preserves benign query parameters`() {
        val initialHost = server.url("/").host
        val redirectedHost = "${if (initialHost == "localhost") "127.0.0.1" else "localhost"}:${server.port}"
        val redirectQuery = listOf(
            "api_key=redirect-token",
            "access_key=access-key-secret",
            "aws_access_key_id=aws-key-secret",
            "consumer_key=consumer-key-secret",
            "access_token=access-secret",
            "password=query-secret",
            "auth=bearer-secret",
            "client_secret=client-secret",
            "client_assertion=assertion-secret",
            "code=oauth-code",
            "code_verifier=oauth-verifier",
            "sid=session-id",
            "PHPSESSID=php-session",
            "JSESSIONID=java-session",
            "session=session-secret",
            "secret=secret-value",
            "pagination_token=page-cursor",
            "continuationToken=next-cursor",
            "next_page_token=following-cursor",
            "signature=signed-target",
            "X-Goog-Signature=google-signature",
            "X-Amz-Credential=aws-credential",
            "X-Amz-Signature=aws-signature",
            "X-Amz-Security-Token=aws-session",
            "monkey=kept",
            "keep=value",
        ).joinToString("&")
        server.enqueue(
            MockResponse().setResponseCode(302).addHeader(
                "Location",
                "http://redirect-user:redirect-password@$redirectedHost/next?$redirectQuery",
            ),
        )
        server.enqueue(MockResponse().setBody("{}"))
        val sessions = CountingSessionRepository(baseUrl = server.url("/").toString())
        val client = NetworkModule.provideOkHttpClient(
            clientIdentityInterceptor = ClientIdentityInterceptor(sessions),
            apiRetryInterceptor = ApiRetryInterceptor(),
            serverUrlInterceptor = ServerUrlInterceptor(sessions),
        )

        val response = client.newCall(
            Request.Builder()
                .url("https://placeholder.example/Users")
                .header("Cookie", "session=session-secret")
                .header("Proxy-Authorization", "Basic proxy-secret")
                .build(),
        ).execute()

        assertEquals(200, response.code)
        response.close()
        assertEquals(2, server.requestCount)
        server.takeRequest()
        val redirectedRequest = server.takeRequest()
        assertEquals(
            "/next?pagination_token=page-cursor&continuationToken=next-cursor&next_page_token=following-cursor" +
                "&signature=signed-target" +
                "&X-Goog-Signature=google-signature&X-Amz-Credential=aws-credential&X-Amz-Signature=aws-signature" +
                "&X-Amz-Security-Token=aws-session&monkey=kept&keep=value",
            redirectedRequest.path,
        )
        assertEquals("", redirectedRequest.requestUrl?.encodedUsername)
        assertEquals("", redirectedRequest.requestUrl?.encodedPassword)
        assertEquals(null, redirectedRequest.getHeader("Authorization"))
        assertEquals(null, redirectedRequest.getHeader("Cookie"))
        assertEquals(null, redirectedRequest.getHeader("Proxy-Authorization"))
    }

    @Test
    fun `Coil client blocks a public HTTP redirect instead of forwarding artwork credentials`() {
        server.enqueue(
            MockResponse()
                .setResponseCode(302)
                .addHeader("Location", "http://203.0.113.11:8096/image?api_key=session-token"),
        )
        val sessions = CountingSessionRepository(baseUrl = "http://127.0.0.1:8096")
        val client = buildAuthenticatedImageClient(
            serverUrlInterceptor = Interceptor { chain -> chain.proceed(chain.request()) },
            clientIdentityInterceptor = ClientIdentityInterceptor(sessions),
        )

        val failure = runCatching {
            client.newCall(Request.Builder().url(server.url("/poster")).build()).execute()
        }.exceptionOrNull()

        assertTrue("the redirect destination must be denied", failure is IOException)
        assertEquals("only the initial LAN request may reach the server", 1, server.requestCount)
        assertTrue(server.takeRequest().getHeader("Authorization").orEmpty().contains("Token=\"session-token\""))
    }

    @Test
    fun `Coil client blocks direct public HTTP before reading artwork credentials`() {
        val sessions = CountingSessionRepository(baseUrl = "https://media.example")
        val client = buildAuthenticatedImageClient(
            serverUrlInterceptor = Interceptor { chain -> chain.proceed(chain.request()) },
            clientIdentityInterceptor = ClientIdentityInterceptor(sessions),
        )

        val failure = runCatching {
            client.newCall(
                Request.Builder()
                    .url("http://203.0.113.13:8096/Items/movie/Images/Primary?api_key=session-token")
                    .build(),
            ).execute()
        }.exceptionOrNull()

        assertTrue("direct artwork URL must be rejected", failure is IOException)
        assertEquals("image request must not read the session token", 0, sessions.tokenReads.get())
        assertEquals("image request must not read the device identity", 0, sessions.deviceReads.get())
        assertEquals("no cleartext image request may reach the network", 0, server.requestCount)
    }

    @Test
    fun `identity interceptor rejects direct public HTTP before token and device reads`() {
        val sessions = CountingSessionRepository(baseUrl = "https://media.example")
        val innerInterceptorCalled = AtomicInteger()
        val client = okhttp3.OkHttpClient.Builder()
            .addInterceptor(ClientIdentityInterceptor(sessions))
            .addInterceptor { chain ->
                innerInterceptorCalled.incrementAndGet()
                Response.Builder()
                    .request(chain.request())
                    .protocol(Protocol.HTTP_1_1)
                    .code(200)
                    .message("OK")
                    .body("unexpected".toResponseBody())
                    .build()
            }
            .build()

        val failure = runCatching {
            client.newCall(Request.Builder().url("http://203.0.113.12:8096/Users").build()).execute()
        }.exceptionOrNull()

        assertTrue(failure is IOException)
        assertEquals(0, sessions.tokenReads.get())
        assertEquals(0, sessions.deviceReads.get())
        assertFalse(innerInterceptorCalled.get() > 0)
    }

    private class CountingSessionRepository(
        private val baseUrl: String,
    ) : SessionRepository {
        val tokenReads = AtomicInteger()
        val deviceReads = AtomicInteger()

        override fun getAccessToken() = flowOf("session-token").also { tokenReads.incrementAndGet() }
        override fun getDeviceId() = flowOf("device-42").also { deviceReads.incrementAndGet() }
        override fun getBaseUrl() = flowOf(baseUrl)
        override fun getCurrentUserId() = flowOf("user-1")
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
