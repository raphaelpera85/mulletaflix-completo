package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import java.util.concurrent.CountDownLatch
import java.util.concurrent.ConcurrentLinkedQueue
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineExceptionHandler
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.cancel
import kotlinx.coroutines.suspendCancellableCoroutine
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.WebSocketListener
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SyncPlayRealtimeClientTest {
    @Test
    fun `start returns while the persisted server url is still loading`() {
        val server = MockWebServer()
        server.start()
        val serverAcceptedSocket = CountDownLatch(1)
        val serverClosedSocket = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: okhttp3.WebSocket, response: okhttp3.Response) {
                    serverAcceptedSocket.countDown()
                }

                override fun onClosing(webSocket: okhttp3.WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }

                override fun onClosed(webSocket: okhttp3.WebSocket, code: Int, reason: String) {
                    serverClosedSocket.countDown()
                }
            }),
        )

        val urlLookupStarted = CountDownLatch(1)
        val allowUrlLookupToFinish = CountDownLatch(1)
        val startReturned = CountDownLatch(1)
        val caller = Executors.newSingleThreadExecutor()
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val client = newClient(
            serverUrl = flow {
                urlLookupStarted.countDown()
                check(allowUrlLookupToFinish.await(5, TimeUnit.SECONDS))
                emit(server.url("/").toString())
            },
            applicationScope = scope,
        )

        try {
            caller.submit {
                client.start("test-group")
                startReturned.countDown()
            }

            assertTrue("server URL lookup did not start", urlLookupStarted.await(5, TimeUnit.SECONDS))
            assertTrue(
                "starting SyncPlay must not wait for persisted session I/O",
                startReturned.await(1, TimeUnit.SECONDS),
            )

            allowUrlLookupToFinish.countDown()
            assertTrue(
                "client should complete the WebSocket handshake after the URL becomes available",
                serverAcceptedSocket.await(5, TimeUnit.SECONDS),
            )
        } finally {
            allowUrlLookupToFinish.countDown()
            client.stop()
            assertTrue("WebSocket close handshake did not finish", serverClosedSocket.await(2, TimeUnit.SECONDS))
            scope.cancel()
            caller.shutdownNow()
            server.shutdown()
        }
    }

    @Test
    fun `start retries when persisted server url lookup fails`() {
        val server = MockWebServer()
        server.start()
        val serverAcceptedSocket = CountDownLatch(1)
        val serverClosedSocket = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: okhttp3.WebSocket, response: okhttp3.Response) {
                    serverAcceptedSocket.countDown()
                }

                override fun onClosing(webSocket: okhttp3.WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }

                override fun onClosed(webSocket: okhttp3.WebSocket, code: Int, reason: String) {
                    serverClosedSocket.countDown()
                }
            }),
        )
        val urlReads = AtomicInteger(0)
        val uncaughtExceptions = ConcurrentLinkedQueue<Throwable>()
        val scope = CoroutineScope(
            SupervisorJob() + Dispatchers.IO + CoroutineExceptionHandler { _, error ->
                uncaughtExceptions.add(error)
            },
        )
        val client = newClient(
            serverUrl = flow {
                if (urlReads.incrementAndGet() == 1) error("temporary session read failure")
                emit(server.url("/").toString())
            },
            applicationScope = scope,
        )

        try {
            client.start("test-group")

            assertTrue(
                "a transient session read failure must retry and complete a WebSocket handshake",
                serverAcceptedSocket.await(5, TimeUnit.SECONDS),
            )
            assertEquals("the failed URL read should be followed by one retry", 2, urlReads.get())
            assertNull("transient lookup failures must be handled by the client", uncaughtExceptions.poll())
        } finally {
            client.stop()
            if (serverAcceptedSocket.count == 0L) {
                assertTrue("WebSocket close handshake did not finish", serverClosedSocket.await(2, TimeUnit.SECONDS))
            }
            scope.cancel()
            server.shutdown()
        }
    }

    @Test
    fun `stop cancels a pending persisted server url lookup`() {
        val urlLookupStarted = CountDownLatch(1)
        val urlLookupCancelled = CountDownLatch(1)
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val client = newClient(
            serverUrl = flow {
                urlLookupStarted.countDown()
                try {
                    suspendCancellableCoroutine<Nothing> { }
                } finally {
                    urlLookupCancelled.countDown()
                }
            },
            applicationScope = scope,
        )

        try {
            client.start("test-group")
            assertTrue("server URL lookup did not start", urlLookupStarted.await(5, TimeUnit.SECONDS))

            client.stop()

            assertTrue(
                "leaving the room must cancel its pending persisted URL lookup",
                urlLookupCancelled.await(2, TimeUnit.SECONDS),
            )
        } finally {
            client.stop()
            scope.cancel()
        }
    }

    private fun newClient(
        serverUrl: Flow<String>,
        applicationScope: CoroutineScope,
    ) = SyncPlayRealtimeClient(
        httpClient = OkHttpClient(),
        sessionRepository = object : SessionRepository {
            override fun getAccessToken() = flowOf("test-token")
            override fun getDeviceId() = flowOf("test-device")
            override fun getBaseUrl() = serverUrl
            override fun getCurrentUserId() = flowOf("test-user")
            override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
            override suspend fun setBaseUrl(url: String) = Unit
            override suspend fun clearSession() = Unit
        },
        applicationScope = applicationScope,
        moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build(),
    )
}
