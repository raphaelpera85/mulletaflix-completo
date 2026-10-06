package org.mulletaflix.core.api

import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.HttpException
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory

/**
 * Contract coverage for the book-reader EPUB download used by
 * `BookReaderViewModel`. Unlike the feedback/remote-playback endpoints, this
 * call carries no `@Tag` session snapshot, so authorization must come from
 * the ambient [SessionRepository] through [ClientIdentityInterceptor] like
 * any other authenticated API call.
 */
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

    private fun api(accessToken: String? = "book-reader-token", deviceId: String = "book-reader-device"): MulletaFlixApiService {
        val session = object : SessionRepository {
            override fun getAccessToken() = flowOf(accessToken)
            override fun getDeviceId() = flowOf(deviceId)
            override fun getBaseUrl() = flowOf(server.url("/").toString().trimEnd('/'))
            override fun getCurrentUserId() = flowOf<String?>("user-1")
            override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
            override suspend fun setBaseUrl(url: String) = Unit
            override suspend fun clearSession() = Unit
        }
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
        val client = OkHttpClient.Builder()
            .addInterceptor(ClientIdentityInterceptor(session))
            .build()

        return Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(moshi))
            .build()
            .create(MulletaFlixApiService::class.java)
    }

    @Test
    fun `epub request hits the book reader route for the given item`() = runBlocking {
        server.enqueue(MockResponse().setBody("epub-bytes").setHeader("Content-Type", "application/epub+zip"))

        api().getBookReaderEpub("item-42").use { it.bytes() }

        val request = server.takeRequest()
        val url = requireNotNull(request.requestUrl)
        assertEquals("GET", request.method)
        assertEquals("/BookReader/Items/item-42/BookReader/Epub", url.encodedPath)
    }

    @Test
    fun `item id is URL-encoded in the request path`() = runBlocking {
        server.enqueue(MockResponse().setBody("epub-bytes"))

        api().getBookReaderEpub("item with spaces/slash").use { it.bytes() }

        val request = server.takeRequest()
        val url = requireNotNull(request.requestUrl)
        // Retrofit path-encodes the segment; the raw value must never leak unescaped into the URL.
        assertTrue(url.encodedPath.contains("item%20with%20spaces%2Fslash"))
    }

    @Test
    fun `request carries the ambient session token and device identity`() = runBlocking {
        server.enqueue(MockResponse().setBody("epub-bytes"))

        api(accessToken = "secret-session-token", deviceId = "device-xyz")
            .getBookReaderEpub("item-1")
            .use { it.bytes() }

        val request = server.takeRequest()
        val authorization = request.getHeader("Authorization").orEmpty()
        assertTrue(authorization.contains("Token=\"secret-session-token\""))
        assertTrue(authorization.contains("DeviceId=\"device-xyz\""))
    }

    @Test
    fun `request without an active session omits the token but keeps device identity`() = runBlocking {
        server.enqueue(MockResponse().setBody("epub-bytes"))

        api(accessToken = null, deviceId = "device-anon")
            .getBookReaderEpub("item-1")
            .use { it.bytes() }

        val request = server.takeRequest()
        val authorization = request.getHeader("Authorization").orEmpty()
        assertTrue(!authorization.contains("Token="))
        assertTrue(authorization.contains("DeviceId=\"device-anon\""))
    }

    @Test
    fun `response streams the exact server payload and reports its content type and length`() = runBlocking {
        val payload = "fake-epub-container-bytes"
        server.enqueue(
            MockResponse()
                .setBody(payload)
                .setHeader("Content-Type", "application/epub+zip"),
        )

        val body = api().getBookReaderEpub("item-7")
        try {
            assertEquals("application/epub+zip", body.contentType()?.toString())
            assertEquals(payload.toByteArray().size.toLong(), body.contentLength())
            assertEquals(payload, body.string())
        } finally {
            body.close()
        }
    }

    @Test
    fun `unsupported format response surfaces its HTTP status to the caller`() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(415).setBody("Unsupported Media Type"))

        val failure = try {
            api().getBookReaderEpub("item-unsupported")
            null
        } catch (httpException: HttpException) {
            httpException
        }

        assertEquals(415, failure?.code())
    }

    @Test
    fun `missing item surfaces 404 to the caller`() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(404))

        val failure = try {
            api().getBookReaderEpub("missing-item")
            null
        } catch (httpException: HttpException) {
            httpException
        }

        assertEquals(404, failure?.code())
    }
}
