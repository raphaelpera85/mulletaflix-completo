package org.mulletaflix.core.api

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.mulletaflix.core.api.di.NetworkModule
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.http.Streaming

class BookReaderHttpContractTest {
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
    fun `book epub uses authenticated streaming endpoint`() = runBlocking {
        val epubBytes = "epub-test-payload".toByteArray()
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setHeader("Content-Type", "application/epub+zip")
                .setBody(okio.Buffer().write(epubBytes)),
        )

        val response = api().getBookEpub("book-123")

        assertTrue(response.isSuccessful)
        val body = response.body()
        assertNotNull(body)
        body!!.use {
            assertEquals("application/epub+zip", it.contentType()?.toString())
            assertTrue(epubBytes.contentEquals(it.bytes()))
        }

        val request = server.takeRequest()
        assertEquals("GET", request.method)
        assertEquals("/BookReader/Items/book-123/BookReader/Epub", request.requestUrl?.encodedPath)
        assertEquals(
            "MediaBrowser Token=\"reader-token\", Client=\"MulletaFlix Android\", " +
                "Device=\"Android\", DeviceId=\"reader-device\", Version=\"${BuildConfig.CLIENT_VERSION}\"",
            request.getHeader("Authorization"),
        )
    }

    @Test
    fun `book epub contract keeps retrofit streaming annotation`() {
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "getBookEpub" }

        assertNotNull(method.getAnnotation(Streaming::class.java))
    }

    private fun api(): MulletaFlixApiService {
        val session = ReaderSessionRepository(server.url("/").toString().trimEnd('/'))
        val client = NetworkModule.provideOkHttpClient(
            clientIdentityInterceptor = ClientIdentityInterceptor(session),
            apiRetryInterceptor = ApiRetryInterceptor(),
            serverUrlInterceptor = ServerUrlInterceptor(session),
        )
        val retrofit = NetworkModule.provideRetrofit(client, NetworkModule.provideMoshi())
        return NetworkModule.provideApiService(retrofit)
    }

    private class ReaderSessionRepository(serverUrl: String) : SessionRepository {
        private val baseUrl = MutableStateFlow(serverUrl)
        private val token = MutableStateFlow<String?>("reader-token")
        private val userId = MutableStateFlow<String?>("reader-user")
        private val deviceId = MutableStateFlow("reader-device")

        override fun getAccessToken() = token
        override fun getDeviceId() = deviceId
        override fun getBaseUrl() = baseUrl
        override fun getCurrentUserId() = userId
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
