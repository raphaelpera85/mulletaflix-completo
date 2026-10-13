package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import java.io.ByteArrayOutputStream
import java.io.DataInputStream
import java.net.InetAddress
import java.net.ServerSocket
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.util.Base64
import java.util.concurrent.CountDownLatch
import java.util.concurrent.ConcurrentLinkedQueue
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineExceptionHandler
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.take
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.cancel
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.withContext
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestCoroutineScheduler
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.WebSocketListener
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SyncPlayRealtimeClientTest {
    @Test
    fun `handshake failure emits disconnected before reconnecting`() = runBlocking {
        val server = MockWebServer()
        server.start()
        val reconnected = CountDownLatch(1)
        server.enqueue(MockResponse().setResponseCode(503))
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    reconnected.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }
            }),
        )

        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val client = newClient(
            serverUrl = flowOf(server.url("/").toString()),
            applicationScope = scope,
        )
        val events = async(start = CoroutineStart.UNDISPATCHED) {
            withTimeout(5_000) { client.events.take(2).toList() }
        }

        try {
            client.start("test-group")

            assertEquals(
                listOf(SyncPlayRealtimeEvent.Disconnected, SyncPlayRealtimeEvent.Connected),
                events.await(),
            )
            assertTrue("client did not reconnect after the failed handshake", reconnected.await(5, TimeUnit.SECONDS))
            assertEquals(2, server.requestCount)
        } finally {
            client.stop()
            scope.cancel()
            server.shutdown()
        }
    }

    @Test
    fun `remote websocket close emits disconnected and reconnects`() = runBlocking {
        val server = MockWebServer()
        server.start()
        val firstConnectionOpened = CountDownLatch(1)
        val closeRemoteSocket = CountDownLatch(1)
        val remoteCloseCompleted = CountDownLatch(1)
        val reconnected = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    firstConnectionOpened.countDown()
                    Thread {
                        if (closeRemoteSocket.await(5, TimeUnit.SECONDS)) {
                            webSocket.close(1000, "server closed room socket")
                        }
                    }.start()
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    remoteCloseCompleted.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }
            }),
        )
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    reconnected.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }
            }),
        )

        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val client = newClient(
            serverUrl = flowOf(server.url("/").toString()),
            applicationScope = scope,
        )
        val events = async(start = CoroutineStart.UNDISPATCHED) {
            withTimeout(7_000) { client.events.take(3).toList() }
        }

        try {
            client.start("test-group")
            assertTrue("test server did not accept the first WebSocket", firstConnectionOpened.await(5, TimeUnit.SECONDS))
            closeRemoteSocket.countDown()

            assertEquals(
                listOf(
                    SyncPlayRealtimeEvent.Connected,
                    SyncPlayRealtimeEvent.Disconnected,
                    SyncPlayRealtimeEvent.Connected,
                ),
                events.await(),
            )
            assertTrue("remote close handshake did not finish", remoteCloseCompleted.await(2, TimeUnit.SECONDS))
            assertTrue("client did not reconnect after the remote close", reconnected.await(5, TimeUnit.SECONDS))
            assertEquals(2, server.requestCount)
        } finally {
            closeRemoteSocket.countDown()
            client.stop()
            scope.cancel()
            server.shutdown()
        }
    }

    @Test
    fun `remote close frame without status is acknowledged with a valid code`() = runBlocking {
        val server = ServerSocket(0, 1, InetAddress.getByName("127.0.0.1"))
        val replyReceived = CountDownLatch(1)
        val replyCode = AtomicInteger(-1)
        val serverFailures = ConcurrentLinkedQueue<Throwable>()
        val serverThread = Thread {
            try {
                server.accept().use { socket ->
                    socket.soTimeout = 5_000
                    val input = DataInputStream(socket.getInputStream())
                    val requestHeaders = ByteArrayOutputStream()
                    var headerTerminator = 0
                    while (headerTerminator < 4) {
                        val byte = input.readUnsignedByte()
                        requestHeaders.write(byte)
                        headerTerminator = when (headerTerminator) {
                            0 -> if (byte == '\r'.code) 1 else 0
                            1 -> when (byte) {
                                '\n'.code -> 2
                                '\r'.code -> 1
                                else -> 0
                            }
                            2 -> if (byte == '\r'.code) 3 else 0
                            else -> when (byte) {
                                '\n'.code -> 4
                                '\r'.code -> 1
                                else -> 0
                            }
                        }
                    }
                    val webSocketKey = requestHeaders.toString(StandardCharsets.US_ASCII)
                        .lineSequence()
                        .first { it.startsWith("Sec-WebSocket-Key:", ignoreCase = true) }
                        .substringAfter(':')
                        .trim()
                    val accept = Base64.getEncoder().encodeToString(
                        MessageDigest.getInstance("SHA-1").digest(
                            (webSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
                                .toByteArray(StandardCharsets.UTF_8),
                        ),
                    )
                    val output = socket.getOutputStream()
                    output.write(
                        ("HTTP/1.1 101 Switching Protocols\r\n" +
                            "Upgrade: websocket\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Sec-WebSocket-Accept: $accept\r\n\r\n")
                            .toByteArray(StandardCharsets.US_ASCII),
                    )
                    output.write(byteArrayOf(0x88.toByte(), 0x00))
                    output.flush()

                    val firstByte = input.readUnsignedByte()
                    val secondByte = input.readUnsignedByte()
                    check(firstByte == 0x88) { "expected a close reply frame, got opcode byte $firstByte" }
                    check(secondByte and 0x80 != 0) { "client close frame must be masked" }
                    val payloadLength = secondByte and 0x7f
                    check(payloadLength == 2) { "expected a close status without a reason" }
                    val mask = ByteArray(4).also(input::readFully)
                    val payload = ByteArray(payloadLength).also(input::readFully)
                    payload.indices.forEach { index -> payload[index] = (payload[index].toInt() xor mask[index % 4].toInt()).toByte() }
                    replyCode.set(((payload[0].toInt() and 0xff) shl 8) or (payload[1].toInt() and 0xff))
                    replyReceived.countDown()
                }
            } catch (failure: Throwable) {
                serverFailures.add(failure)
                replyReceived.countDown()
            }
        }.apply {
            isDaemon = true
            start()
        }

        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val client = newClient(
            serverUrl = flowOf("http://127.0.0.1:${server.localPort}/"),
            applicationScope = scope,
        )
        val events = async(start = CoroutineStart.UNDISPATCHED) {
            withTimeout(5_000) { client.events.take(2).toList() }
        }

        try {
            client.start("test-group")
            assertEquals(
                listOf(SyncPlayRealtimeEvent.Connected, SyncPlayRealtimeEvent.Disconnected),
                events.await(),
            )
            assertTrue("client did not reply to the close frame without a status", replyReceived.await(2, TimeUnit.SECONDS))
            assertNull("raw WebSocket peer failed", serverFailures.poll())
            assertEquals("an omitted status must be answered with a legal normal-closure status", 1000, replyCode.get())
        } finally {
            client.stop()
            scope.cancel()
            server.close()
            serverThread.join(1_000)
        }
    }

    @Test
    fun `stop cancels reconnect scheduled after remote websocket close`() = runBlocking {
        val server = MockWebServer()
        server.start()
        val firstConnectionOpened = CountDownLatch(1)
        val closeRemoteSocket = CountDownLatch(1)
        val remoteCloseCompleted = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    firstConnectionOpened.countDown()
                    Thread {
                        if (closeRemoteSocket.await(5, TimeUnit.SECONDS)) {
                            webSocket.close(1000, "server closed room socket")
                        }
                    }.apply {
                        isDaemon = true
                        start()
                    }
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    remoteCloseCompleted.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }
            }),
        )

        val scheduler = TestCoroutineScheduler()
        val reconnectDispatcher = StandardTestDispatcher(scheduler)
        val scope = CoroutineScope(SupervisorJob() + reconnectDispatcher)
        val client = newClient(
            serverUrl = flowOf(server.url("/").toString()),
            applicationScope = scope,
        )
        val events = async(start = CoroutineStart.UNDISPATCHED) {
            withTimeout(5_000) { client.events.take(2).toList() }
        }
        val scopeJob = requireNotNull(scope.coroutineContext[Job])

        try {
            client.start("test-group")
            scheduler.runCurrent()
            assertTrue("test server did not accept the WebSocket", firstConnectionOpened.await(5, TimeUnit.SECONDS))
            scheduler.runCurrent()
            assertTrue("initial URL/socket job should finish after opening the connection", scopeJob.children.none { it.isActive })
            closeRemoteSocket.countDown()
            assertEquals(
                listOf(SyncPlayRealtimeEvent.Connected, SyncPlayRealtimeEvent.Disconnected),
                events.await(),
            )
            assertTrue("remote close handshake did not finish", remoteCloseCompleted.await(2, TimeUnit.SECONDS))

            var retryJob: Job? = null
            withTimeout(2_000) {
                while (retryJob == null) {
                    scheduler.runCurrent()
                    retryJob = scopeJob.children.singleOrNull { it.isActive }
                    if (retryJob == null) delay(5)
                }
            }
            val scheduledRetry = requireNotNull(retryJob)
            assertTrue("retry coroutine must be active while its virtual backoff is suspended", scheduledRetry.isActive)
            client.stop()
            assertFalse("stop must cancel the scheduled retry job", scheduledRetry.isActive)
            scheduler.advanceTimeBy(2_000)
            scheduler.runCurrent()

            assertEquals("a canceled retry must not issue another WebSocket request", 1, server.requestCount)
            assertFalse(client.connectionState.value.connected)
        } finally {
            closeRemoteSocket.countDown()
            client.stop()
            scope.cancel()
            server.shutdown()
        }
    }

    @Test
    fun `websocket handshake and command are delivered through the events flow`() = runBlocking {
        val server = MockWebServer()
        server.start()
        val serverAcceptedSocket = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    serverAcceptedSocket.countDown()
                    webSocket.send(
                        """{"MessageType":"SyncPlayCommand","Data":{"GroupId":"test-group","PlaylistItemId":"item-42","Command":"Pause","PositionTicks":123}}""",
                    )
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }
            }),
        )

        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val client = newClient(
            serverUrl = flowOf(server.url("/").toString()),
            applicationScope = scope,
        )
        val events = async(start = CoroutineStart.UNDISPATCHED) {
            withTimeout(5_000) { client.events.take(2).toList() }
        }

        try {
            client.start("test-group")
            val handshakeAccepted = withContext(Dispatchers.IO) {
                serverAcceptedSocket.await(5, TimeUnit.SECONDS)
            }
            assertTrue("test server did not accept the WebSocket handshake", handshakeAccepted)

            assertEquals(
                listOf(
                    SyncPlayRealtimeEvent.Connected,
                    SyncPlayRealtimeEvent.Command(
                        groupId = "test-group",
                        playlistItemId = "item-42",
                        command = "Pause",
                        positionTicks = 123L,
                    ),
                ),
                events.await(),
            )
        } finally {
            client.stop()
            scope.cancel()
            server.shutdown()
        }
    }

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
