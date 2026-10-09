package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import java.io.IOException
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.Proxy
import java.net.Socket
import java.net.ProxySelector
import javax.net.SocketFactory
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import okhttp3.Dns
import okhttp3.Connection
import okhttp3.Address
import okhttp3.Authenticator
import okhttp3.CertificatePinner
import okhttp3.ConnectionSpec
import okhttp3.Interceptor
import okhttp3.Protocol
import okhttp3.Route
import okhttp3.Request
import okhttp3.Response
import okhttp3.CookieJar
import okhttp3.Cache
import okhttp3.ConnectionPool
import okhttp3.EventListener
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
import org.mulletaflix.core.api.di.NetworkModule
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy
import org.mulletaflix.core.common.network.LocalNetworkCleartextNetworkInterceptor

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
    fun `real Retrofit call blocks public HTTP redirect before second request`() = runBlocking {
        server.enqueue(
            MockResponse()
                .setResponseCode(302)
                .addHeader(
                    "Location",
                    "http://public-http.test:${server.port}/System/Info?api_key=redirect-token",
                ),
        )
        val sessions = CountingSessionRepository(baseUrl = server.url("/").toString())
        val client = NetworkModule.provideOkHttpClient(
            clientIdentityInterceptor = ClientIdentityInterceptor(sessions),
            apiRetryInterceptor = ApiRetryInterceptor(),
            serverUrlInterceptor = ServerUrlInterceptor(sessions),
        ).newBuilder()
            // Keep this integration test hermetic even if redirect protection regresses.
            .dns(object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    if (hostname == "public-http.test") listOf(InetAddress.getByName("127.0.0.1"))
                    else Dns.SYSTEM.lookup(hostname)
            })
            .build()
        val api = NetworkModule.provideRetrofit(client, Moshi.Builder().build())
            .create(MulletaFlixApiService::class.java)

        val failure = runCatching { api.getSystemInfo() }.exceptionOrNull()

        assertTrue("Retrofit must reject the public HTTP redirect", failure is IOException)
        assertEquals("only the initial LAN request may reach the fixture", 1, server.requestCount)
        assertEquals("the session is read only for the initial local request", 1, sessions.tokenReads.get())
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

    @Test
    fun `local DNS identity is not read before the connected route passes cleartext validation`() {
        server.enqueue(MockResponse().setBody("{}"))
        val sessions = CountingSessionRepository(baseUrl = "https://media.example")
        val identityReadsAtRouteBoundary = AtomicInteger(-1)
        val identityInterceptor = ClientIdentityInterceptor(sessions)
        val url = server.url("/Users").newBuilder().host("media.local").build()
        val client = okhttp3.OkHttpClient.Builder()
            .addInterceptor(identityInterceptor)
            .enforceLocalNetworkCleartextPolicy()
            .addNetworkInterceptor { chain ->
                identityReadsAtRouteBoundary.set(sessions.tokenReads.get() + sessions.deviceReads.get())
                chain.proceed(chain.request())
            }
            .addNetworkInterceptor(identityInterceptor.identityAfterConnectedRoute())
            .dns(object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    if (hostname == "media.local") listOf(InetAddress.getByName("127.0.0.1"))
                    else Dns.SYSTEM.lookup(hostname)
            })
            .build()

        val response = client.newCall(Request.Builder().url(url).build()).execute()

        assertEquals(200, response.code)
        response.close()
        assertEquals("the connected-route guard must run before reading credentials", 0, identityReadsAtRouteBoundary.get())
        assertEquals("credentials are read after the LAN route is accepted", 1, sessions.tokenReads.get())
        assertEquals(1, sessions.deviceReads.get())
        assertTrue(server.takeRequest().getHeader("Authorization").orEmpty().contains("Token=\"session-token\""))
    }

    @Test
    fun `local DNS route resolving outside the LAN is rejected before token or device reads`() {
        val sessions = CountingSessionRepository(baseUrl = "https://media.example")
        val identity = ClientIdentityInterceptor(sessions)
        val request = Request.Builder().url("http://media.local:8096/Users").build()
        var deferredRequest: Request? = null
        val applicationChain = TestInterceptorChain(request, connection = null) { forwarded ->
            deferredRequest = forwarded
            successfulResponse(forwarded)
        }

        identity.intercept(applicationChain)

        val localAddress = InetAddress.getByName("127.0.0.1")
        val route = Route(
            Address(
                "media.local",
                8096,
                Dns.SYSTEM,
                SocketFactory.getDefault(),
                null,
                null,
                CertificatePinner.DEFAULT,
                Authenticator.NONE,
                Proxy.NO_PROXY,
                listOf(Protocol.HTTP_1_1),
                listOf(ConnectionSpec.CLEARTEXT),
                requireNotNull(ProxySelector.getDefault()),
            ),
            Proxy.NO_PROXY,
            InetSocketAddress("198.51.100.22", 8096),
        )
        val connection = TestConnection(route, TestSocket(localAddress))
        var terminalReached = false
        val networkChain = TestInterceptorChain(
            currentRequest = requireNotNull(deferredRequest),
            connection = connection,
            interceptors = listOf(identity.identityAfterConnectedRoute()),
        ) { forwarded ->
            terminalReached = true
            successfulResponse(forwarded)
        }

        val failure = runCatching {
            LocalNetworkCleartextNetworkInterceptor.intercept(networkChain)
        }.exceptionOrNull()

        assertTrue("A .local name resolving outside the active LAN must be rejected", failure is IOException)
        assertFalse("The identity interceptor must not reach the terminal exchange", terminalReached)
        assertEquals("a rejected route must not read the session token", 0, sessions.tokenReads.get())
        assertEquals("a rejected route must not read the device identity", 0, sessions.deviceReads.get())
    }

    @Test
    fun `local DNS same origin redirect retains deferred session identity`() {
        server.enqueue(MockResponse().setResponseCode(302).addHeader("Location", "/next"))
        server.enqueue(MockResponse().setBody("{}"))
        val sessions = CountingSessionRepository(baseUrl = "https://media.example")
        val identity = ClientIdentityInterceptor(sessions)
        val client = okhttp3.OkHttpClient.Builder()
            .addInterceptor(identity)
            .enforceLocalNetworkCleartextPolicy()
            .addNetworkInterceptor(identity.identityAfterConnectedRoute())
            .dns(localMediaDns())
            .build()
        val url = server.url("/start").newBuilder().host("media.local").build()

        val response = client.newCall(Request.Builder().url(url).build()).execute()

        assertEquals(200, response.code)
        response.close()
        assertEquals(2, server.requestCount)
        repeat(2) {
            assertTrue(
                server.takeRequest().getHeader("Authorization").orEmpty()
                    .contains("Token=\"session-token\""),
            )
        }
        assertEquals(1, sessions.tokenReads.get())
        assertEquals(1, sessions.deviceReads.get())
    }

    @Test
    fun `cross origin local DNS redirect never reattaches deferred session identity`() {
        server.enqueue(
            MockResponse().setResponseCode(302)
                .addHeader("Location", "http://other.local:${server.port}/next?keep=yes"),
        )
        server.enqueue(MockResponse().setBody("{}"))
        val sessions = CountingSessionRepository(baseUrl = "https://media.example")
        val identity = ClientIdentityInterceptor(sessions)
        val client = okhttp3.OkHttpClient.Builder()
            .addInterceptor(identity)
            .enforceLocalNetworkCleartextPolicy()
            .addNetworkInterceptor(identity.identityAfterConnectedRoute())
            .dns(localMediaDns())
            .build()
        val url = server.url("/start").newBuilder().host("media.local").build()

        val response = client.newCall(Request.Builder().url(url).build()).execute()

        assertEquals(200, response.code)
        response.close()
        assertEquals(2, server.requestCount)
        val initial = server.takeRequest()
        val redirected = server.takeRequest()
        assertTrue(initial.getHeader("Authorization").orEmpty().contains("Token=\"session-token\""))
        assertEquals(null, redirected.getHeader("Authorization"))
        assertEquals("keep=yes", redirected.requestUrl?.query)
        assertEquals(1, sessions.tokenReads.get())
        assertEquals(1, sessions.deviceReads.get())
    }

    private fun localMediaDns() = object : Dns {
        override fun lookup(hostname: String): List<InetAddress> =
            if (hostname.endsWith(".local")) listOf(InetAddress.getByName("127.0.0.1"))
            else Dns.SYSTEM.lookup(hostname)
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

    private fun successfulResponse(request: Request): Response = Response.Builder()
        .request(request)
        .protocol(Protocol.HTTP_1_1)
        .code(200)
        .message("OK")
        .body(byteArrayOf().toResponseBody())
        .build()

    private class TestInterceptorChain(
        private var currentRequest: Request,
        private val connection: Connection?,
        private val interceptors: List<Interceptor> = emptyList(),
        private val terminal: (Request) -> Response,
    ) : Interceptor.Chain {
        private var nextInterceptorIndex = 0
        private val delegate = okhttp3.OkHttpClient().newCall(currentRequest)

        override fun request(): Request = currentRequest

        override fun proceed(request: Request): Response {
            currentRequest = request
            val nextInterceptor = interceptors.getOrNull(nextInterceptorIndex++)
                ?: return terminal(request)
            return nextInterceptor.intercept(this)
        }

        override fun connection(): Connection? = connection
        override fun connectTimeoutMillis(): Int = 10_000
        override fun withConnectTimeout(timeout: Int, unit: java.util.concurrent.TimeUnit): Interceptor.Chain = this
        override fun readTimeoutMillis(): Int = 10_000
        override fun withReadTimeout(timeout: Int, unit: java.util.concurrent.TimeUnit): Interceptor.Chain = this
        override fun writeTimeoutMillis(): Int = 10_000
        override fun withWriteTimeout(timeout: Int, unit: java.util.concurrent.TimeUnit): Interceptor.Chain = this
        override val followSslRedirects: Boolean = true
        override val followRedirects: Boolean = true
        override val dns: Dns = Dns.SYSTEM
        override val socketFactory: SocketFactory = SocketFactory.getDefault()
        override val retryOnConnectionFailure: Boolean = true
        override val authenticator: Authenticator = Authenticator.NONE
        override val cookieJar: CookieJar = CookieJar.NO_COOKIES
        override val cache: Cache? = null
        override val proxy: Proxy? = Proxy.NO_PROXY
        override val proxySelector: ProxySelector = requireNotNull(ProxySelector.getDefault())
        override val proxyAuthenticator: Authenticator = Authenticator.NONE
        override val sslSocketFactoryOrNull: javax.net.ssl.SSLSocketFactory? = null
        override val x509TrustManagerOrNull: javax.net.ssl.X509TrustManager? = null
        override val hostnameVerifier: javax.net.ssl.HostnameVerifier = javax.net.ssl.HttpsURLConnection.getDefaultHostnameVerifier()
        override val certificatePinner: CertificatePinner = CertificatePinner.DEFAULT
        override val connectionPool: ConnectionPool = okhttp3.ConnectionPool()
        override val eventListener: EventListener = EventListener.NONE

        override fun withDns(dns: Dns): Interceptor.Chain = this
        override fun withSocketFactory(socketFactory: SocketFactory): Interceptor.Chain = this
        override fun withRetryOnConnectionFailure(retryOnConnectionFailure: Boolean): Interceptor.Chain = this
        override fun withAuthenticator(authenticator: Authenticator): Interceptor.Chain = this
        override fun withCookieJar(cookieJar: CookieJar): Interceptor.Chain = this
        override fun withCache(cache: Cache?): Interceptor.Chain = this
        override fun withProxy(proxy: Proxy?): Interceptor.Chain = this
        override fun withProxySelector(proxySelector: ProxySelector): Interceptor.Chain = this
        override fun withProxyAuthenticator(proxyAuthenticator: Authenticator): Interceptor.Chain = this
        override fun withSslSocketFactory(
            sslSocketFactory: javax.net.ssl.SSLSocketFactory?,
            x509TrustManager: javax.net.ssl.X509TrustManager?,
        ): Interceptor.Chain = this
        override fun withHostnameVerifier(hostnameVerifier: javax.net.ssl.HostnameVerifier): Interceptor.Chain = this
        override fun withCertificatePinner(certificatePinner: CertificatePinner): Interceptor.Chain = this
        override fun withConnectionPool(connectionPool: ConnectionPool): Interceptor.Chain = this

        override fun call(): okhttp3.Call = delegate
    }

    private class TestConnection(
        private val routeValue: Route,
        private val socketValue: Socket,
    ) : Connection {
        override fun route(): Route = routeValue
        override fun socket(): Socket = socketValue
        override fun handshake(): okhttp3.Handshake? = null
        override fun protocol(): Protocol = Protocol.HTTP_1_1
    }

    private class TestSocket(private val boundLocalAddress: InetAddress) : Socket() {
        override fun getLocalAddress(): InetAddress = boundLocalAddress
    }
}
