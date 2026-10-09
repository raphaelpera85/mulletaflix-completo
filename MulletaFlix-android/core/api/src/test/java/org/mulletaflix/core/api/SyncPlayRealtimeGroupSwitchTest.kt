package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class SyncPlayRealtimeGroupSwitchTest {
    @Test
    fun `new room socket connects before previous room socket finishes closing`() {
        val server = MockWebServer()
        server.start()
        val oldSocketClosing = CountDownLatch(1)
        val allowOldSocketClose = CountDownLatch(1)
        val oldSocketClosed = CountDownLatch(1)
        val firstSocketOpened = CountDownLatch(1)
        val secondSocketOpened = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    firstSocketOpened.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    oldSocketClosing.countDown()
                    assertTrue("test did not release old socket close", allowOldSocketClose.await(5, TimeUnit.SECONDS))
                    webSocket.close(code, reason)
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    oldSocketClosed.countDown()
                }
            }),
        )
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    secondSocketOpened.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }
            }),
        )
        val client = SyncPlayRealtimeClient(
            httpClient = OkHttpClient(),
            sessionRepository = TestSessionRepository(server.url("/").toString()),
            applicationScope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined),
            moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build(),
        )

        try {
            client.start("group-old")
            assertTrue("first room did not connect", firstSocketOpened.await(5, TimeUnit.SECONDS))
            runBlocking { withTimeout(5_000) { client.connectionState.first { it.connected } } }

            client.start("group-new")
            assertTrue("old socket did not begin closing", oldSocketClosing.await(5, TimeUnit.SECONDS))
            assertTrue("new room did not connect", secondSocketOpened.await(5, TimeUnit.SECONDS))
            runBlocking {
                withTimeout(5_000) {
                    client.connectionState.first { it.connected && it.generation >= 4L }
                }
            }
            allowOldSocketClose.countDown()
            assertTrue("old socket close handshake did not finish", oldSocketClosed.await(5, TimeUnit.SECONDS))
            assertEquals("switching rooms should create exactly two sockets", 2, server.requestCount)
        } finally {
            allowOldSocketClose.countDown()
            client.stop()
            server.shutdown()
        }
    }

    private class TestSessionRepository(
        private val serverUrl: String,
    ) : SessionRepository {
        override fun getAccessToken() = flowOf("test-token")
        override fun getDeviceId() = flowOf("test-device")
        override fun getBaseUrl() = flowOf(serverUrl)
        override fun getCurrentUserId() = flowOf("test-user")
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
