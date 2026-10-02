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
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

class PublicServerVerificationHttpTest {
    private lateinit var currentServer: MockWebServer
    private lateinit var candidateServer: MockWebServer

    @Before
    fun setUp() {
        currentServer = MockWebServer().also { it.start() }
        candidateServer = MockWebServer().also { it.start() }
    }

    @After
    fun tearDown() {
        currentServer.shutdown()
        candidateServer.shutdown()
    }

    private fun api(): MulletaFlixApiService {
        val session = object : SessionRepository {
            override fun getAccessToken() = flowOf<String?>("old-server-secret")
            override fun getDeviceId() = flowOf("verification-device")
            override fun getBaseUrl() = flowOf(currentServer.url("/").toString().trimEnd('/'))
            override fun getCurrentUserId() = flowOf<String?>("old-user")
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

    @Test
    fun `verifica endpoint candidato sem enviar token e sem mudar host compartilhado`() = runBlocking {
        candidateServer.enqueue(
            MockResponse().setBody("""{"Id":"candidate-id","ServerName":"Candidate"}"""),
        )

        val result = api().getPublicSystemInfo(
            PublicServerVerificationRequest(candidateServer.url("/").toString().trimEnd('/')),
        )
        val request = candidateServer.takeRequest()

        assertEquals("candidate-id", result.id)
        assertEquals("/System/Info/Public", request.path)
        val authorization = request.getHeader("Authorization").orEmpty()
        assertTrue(authorization.contains("Client=\"MulletaFlix Android\""))
        assertFalse(authorization.contains("Token="))
        assertFalse(authorization.contains("verification-device"))
        assertEquals(0, currentServer.requestCount)
    }
}
