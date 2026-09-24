package org.mulletaflix.core.api

import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

/**
 * Exercises the real Retrofit request used by Quick Connect.
 *
 * The server rejects an anonymous POST to QuickConnect/Initiate. Keeping this
 * contract at the HTTP boundary prevents a future interceptor or Retrofit
 * wiring change from making the UI look broken while the endpoint is healthy.
 */
class QuickConnectHttpContractTest {

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

    private fun session() = object : SessionRepository {
        override fun getAccessToken() = flowOf<String?>(null)
        override fun getDeviceId() = flowOf("quick-connect-device")
        override fun getBaseUrl() = flowOf(server.url("/").toString().trimEnd('/'))
        override fun getCurrentUserId() = flowOf<String?>(null)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private fun api(): MulletaFlixApiService {
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
        val client = OkHttpClient.Builder()
            .addInterceptor(ClientIdentityInterceptor(session()))
            .build()
        return Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(moshi))
            .build()
            .create(MulletaFlixApiService::class.java)
    }

    @Test
    fun `initiate sends the identity header required by the server`() = runBlocking {
        server.enqueue(
            MockResponse()
                .setResponseCode(200)
                .setBody("""{"Authenticated":false,"Secret":"secret-1","Code":"393877"}"""),
        )

        val result = api().initiateQuickConnect()
        val request = server.takeRequest()
        val authorization = request.getHeader("Authorization")

        assertEquals("393877", result.code)
        assertEquals("secret-1", result.secret)
        assertEquals("POST", request.method)
        assertEquals("/QuickConnect/Initiate", request.path)
        assertNotNull(authorization)
        assertTrue(authorization!!.contains("Client=\"MulletaFlix Android\""))
        assertTrue(authorization.contains("DeviceId=\"quick-connect-device\""))
        assertTrue(!authorization.contains("Token="))
    }
}
