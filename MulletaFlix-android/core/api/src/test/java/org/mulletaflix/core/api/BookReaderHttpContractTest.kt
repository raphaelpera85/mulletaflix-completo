package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
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
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

class BookReaderHttpContractTest {

    private lateinit var server: MockWebServer

    @Before
    fun setUp() {
        server = MockWebServer().also { it.start() }
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    @Test
    fun `book download uses exact path and authenticated client identity`() = runBlocking {
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setHeader("Content-Type", "application/epub+zip")
                .setBody("epub-body"),
        )

        val response = api().getBookEpub("book-123")
        response.body()?.close()
        val request = server.takeRequest()

        assertEquals("/BookReader/Items/book-123/BookReader/Epub", request.path)
        val authorization = request.getHeader("Authorization").orEmpty()
        assertTrue(authorization.contains("Client=\"MulletaFlix Android\""))
        assertTrue(authorization.contains("DeviceId=\"reader-device\""))
        assertTrue(authorization.contains("Token=\"reader-token\""))
    }

    private fun api(): MulletaFlixApiService {
        val session = object : SessionRepository {
            override fun getAccessToken() = flowOf<String?>("reader-token")
            override fun getDeviceId() = flowOf("reader-device")
            override fun getBaseUrl() = flowOf(server.url("/").toString().trimEnd('/'))
            override fun getCurrentUserId() = flowOf<String?>("reader-user")
            override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
            override suspend fun setBaseUrl(url: String) = Unit
            override suspend fun clearSession() = Unit
        }
        val client = OkHttpClient.Builder()
            .addInterceptor(ServerUrlInterceptor(session))
            .addInterceptor(ClientIdentityInterceptor(session))
            .build()
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()

        return Retrofit.Builder()
            .baseUrl("http://localhost:8096/")
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(moshi))
            .build()
            .create(MulletaFlixApiService::class.java)
    }
}
