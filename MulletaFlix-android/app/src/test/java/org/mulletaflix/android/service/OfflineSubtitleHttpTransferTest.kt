package org.mulletaflix.android.service

import com.sun.net.httpserver.HttpServer
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import java.io.File
import java.net.InetSocketAddress
import java.net.URI
import java.util.concurrent.atomic.AtomicReference

class OfflineSubtitleHttpTransferTest {
    @get:Rule
    val temporaryFolder = TemporaryFolder()

    private val session = FeedbackRequestSession(
        serverUrl = "http://127.0.0.1",
        accessToken = "test-token",
        userId = "user-a",
        deviceId = "device-a",
        serverId = "server-a",
    )
    private val subtitle = DownloadSubtitleMetadata(
        streamIndex = 3,
        mimeType = "application/x-subrip",
        language = "pt-BR",
        label = "Português",
        isDefault = false,
        isForced = false,
    )

    @Test
    fun `successful HTTP transfer stores retrievable subtitle only in its download scope`() = runBlocking {
        val content = "1\n00:00:01,000 --> 00:00:02,000\nOlá\n"
        localServer(200, content.toByteArray()).use { server ->
            val store = OfflineSubtitleStore(File(temporaryFolder.root, "captions"))
            val request = authenticatedSubtitleRequest(
                "${server.baseUrl}/Items/movie%2Fone/Subtitles/3/Stream?MediaSourceId=source-1&api_key=test-token",
                session,
            )
            val observedAuthorization = AtomicReference<String?>()
            val client = offlineSubtitleHttpClient(Interceptor { chain ->
                observedAuthorization.set(chain.request().tag(FeedbackRequestSession::class.java)?.accessToken)
                chain.proceed(chain.request())
            })

            val result = transferOfflineSubtitle(
                call = client.newCall(request),
                isSessionCurrent = { true },
                persistBody = { body -> store.store("server-a", "user-a", "download-a", subtitle, body) },
            )

            assertTrue(result is OfflineSubtitleTransferResult.Stored)
            assertEquals("test-token", observedAuthorization.get())
            assertEquals("/Items/movie%2Fone/Subtitles/3/Stream", server.requestPath)
            assertEquals("MediaSourceId=source-1&api_key=test-token", server.query)
            val uri = store.uriFor("server-a", "user-a", "download-a", subtitle)
            assertEquals(content, File(URI(requireNotNull(uri))).readText())
            val entry = store.entries("server-a", "user-a", "download-a", listOf(subtitle)).single()
            assertEquals(subtitle.streamIndex, entry.streamIndex)
            assertEquals(subtitle.mimeType, entry.mimeType)
            assertEquals(subtitle.language, entry.language)
            assertEquals(subtitle.label, entry.label)
            assertNull(store.uriFor("server-a", "other-user", "download-a", subtitle))
            assertNull(store.uriFor("server-a", "user-a", "another-download", subtitle))
        }
    }

    @Test
    fun `HTTP failure preserves retry classification and writes no subtitle`() = runBlocking {
        localServer(503, "temporarily unavailable".toByteArray()).use { server ->
            val store = OfflineSubtitleStore(File(temporaryFolder.root, "captions"))
            val client = offlineSubtitleHttpClient(Interceptor { chain -> chain.proceed(chain.request()) })
            val result = transferOfflineSubtitle(
                call = client.newCall(authenticatedSubtitleRequest("${server.baseUrl}/subtitles/3", session)),
                isSessionCurrent = { true },
                persistBody = { body -> store.store("server-a", "user-a", "download-a", subtitle, body) },
            )

            assertEquals(OfflineSubtitleTransferResult.HttpFailure(503), result)
            assertTrue(shouldRetryOfflineSubtitleHttpStatus(503))
            assertNull(store.uriFor("server-a", "user-a", "download-a", subtitle))
        }
    }

    @Test
    fun `invalid HTTP subtitle never commits a partial cache file`() = runBlocking {
        localServer(200, "<!doctype html><html>login</html>".toByteArray()).use { server ->
            val store = OfflineSubtitleStore(File(temporaryFolder.root, "captions"))
            val client = offlineSubtitleHttpClient(Interceptor { chain -> chain.proceed(chain.request()) })
            val result = transferOfflineSubtitle(
                call = client.newCall(authenticatedSubtitleRequest("${server.baseUrl}/subtitles/3", session)),
                isSessionCurrent = { true },
                persistBody = { body -> store.store("server-a", "user-a", "download-a", subtitle, body) },
            )

            assertEquals(OfflineSubtitleTransferResult.NotStored, result)
            assertNull(store.uriFor("server-a", "user-a", "download-a", subtitle))
            assertFalse(
                File(temporaryFolder.root, "captions")
                    .listFiles()
                    .orEmpty()
                    .any { it.name.endsWith(".pending") },
            )
        }
    }

    @Test
    fun `session changing after HTTP response prevents persistence`() = runBlocking {
        localServer(200, "valid subtitle".toByteArray()).use { server ->
            val store = OfflineSubtitleStore(File(temporaryFolder.root, "captions"))
            val client = offlineSubtitleHttpClient(Interceptor { chain -> chain.proceed(chain.request()) })
            val result = transferOfflineSubtitle(
                call = client.newCall(authenticatedSubtitleRequest("${server.baseUrl}/subtitles/3", session)),
                isSessionCurrent = { false },
                persistBody = { body -> store.store("server-a", "user-a", "download-a", subtitle, body) },
            )

            assertEquals(OfflineSubtitleTransferResult.SessionChanged, result)
            assertNull(store.uriFor("server-a", "user-a", "download-a", subtitle))
        }
    }

    private fun localServer(status: Int, responseBody: ByteArray): LocalSubtitleServer =
        LocalSubtitleServer(status, responseBody)

    private class LocalSubtitleServer(status: Int, responseBody: ByteArray) : AutoCloseable {
        private val pathRef = AtomicReference<String?>()
        private val queryRef = AtomicReference<String?>()
        private val server = HttpServer.create(InetSocketAddress("127.0.0.1", 0), 0).apply {
            createContext("/") { exchange ->
                pathRef.set(exchange.requestURI.rawPath)
                queryRef.set(exchange.requestURI.rawQuery)
                exchange.sendResponseHeaders(status, responseBody.size.toLong())
                exchange.responseBody.use { it.write(responseBody) }
            }
            start()
        }

        val baseUrl: String get() = "http://127.0.0.1:${server.address.port}"
        val requestPath: String? get() = pathRef.get()
        val query: String? get() = queryRef.get()

        override fun close() = server.stop(0)
    }
}
