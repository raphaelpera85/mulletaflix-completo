package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.flowOf
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy
import org.junit.Test

class SyncPlayCleartextPolicyTest {
    @Test
    fun publicHttpServerIsRejectedBeforeSyncPlayReadsCredentials() {
        val sessions = CountingSessionRepository("http://203.0.113.25:8096")
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
        val client = SyncPlayRealtimeClient(
            httpClient = OkHttpClient(),
            sessionRepository = sessions,
            applicationScope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined),
            moshi = moshi,
        )

        client.start("group-1")

        assertEquals(0, sessions.accessTokenReads.get())
        assertEquals(0, sessions.deviceIdReads.get())
        assertFalse(client.connectionState.value.connected)
    }

    @Test
    fun syncPlaySendsCredentialsInAuthorizationHeaderNotWebSocketUrl() {
        val server = MockWebServer()
        server.start()
        val handshakeCompleted = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    handshakeCompleted.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }
            }),
        )
        val sessions = CountingSessionRepository(server.url("/").toString())
        val httpClient = OkHttpClient.Builder()
            .addInterceptor(ClientIdentityInterceptor(sessions))
            .enforceLocalNetworkCleartextPolicy()
            .build()
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
        val syncPlayClient = SyncPlayRealtimeClient(
            httpClient = httpClient,
            sessionRepository = sessions,
            applicationScope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined),
            moshi = moshi,
        )

        try {
            syncPlayClient.start("group-1")
            assertTrue("WebSocket upgrade did not complete", handshakeCompleted.await(5, TimeUnit.SECONDS))
            val request = server.takeRequest(5, TimeUnit.SECONDS)
            assertNotNull("WebSocket handshake did not reach the test server", request)
            assertEquals("/socket", request!!.path)
            assertFalse(request.path.orEmpty().contains("secret-token"))
            assertEquals(
                buildMediaBrowserAuthorizationHeader("secret-token", "device-1"),
                request.getHeader("Authorization"),
            )
        } finally {
            syncPlayClient.stop()
            server.shutdown()
        }
    }

    private class CountingSessionRepository(
        private val serverUrl: String,
    ) : SessionRepository {
        val accessTokenReads = AtomicInteger()
        val deviceIdReads = AtomicInteger()

        override fun getAccessToken() = flowOf("secret-token").also { accessTokenReads.incrementAndGet() }
        override fun getDeviceId() = flowOf("device-1").also { deviceIdReads.incrementAndGet() }
        override fun getBaseUrl() = flowOf(serverUrl)
        override fun getCurrentUserId() = flowOf("user-1")
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
