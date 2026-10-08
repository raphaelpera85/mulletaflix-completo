package org.mulletaflix.core.api

import androidx.test.ext.junit.runners.AndroidJUnit4
import java.net.InetAddress
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.flowOf
import okhttp3.Dns
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy

@RunWith(AndroidJUnit4::class)
class SyncPlayWebSocketAuthenticationIntegrationTest {
    @Test
    fun httpsHandshakeKeepsCredentialsOutOfUrlAndAuthenticatesByHeader() {
        val tls = tlsFixtures()
        val server = MockWebServer().apply {
            useHttps(tls.server.sslSocketFactory(), false)
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
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
        val sessions = TestSessionRepository(
            server.url("/").newBuilder().host(TEST_HOST).build().toString(),
        )
        val httpClient = OkHttpClient.Builder()
            .addInterceptor(ClientIdentityInterceptor(sessions))
            .sslSocketFactory(tls.client.sslSocketFactory(), tls.client.trustManager)
            .dns(object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    if (hostname == TEST_HOST) listOf(InetAddress.getByName("127.0.0.1"))
                    else Dns.SYSTEM.lookup(hostname)
            })
            .enforceLocalNetworkCleartextPolicy()
            .build()
        val client = SyncPlayRealtimeClient(
            httpClient = httpClient,
            sessionRepository = sessions,
            applicationScope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined),
            moshi = com.squareup.moshi.Moshi.Builder()
                .addLast(com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory())
                .build(),
        )

        try {
            client.start("group-1")
            assertTrue("HTTPS WebSocket upgrade did not complete", handshakeCompleted.await(5, TimeUnit.SECONDS))

            val request = server.takeRequest(5, TimeUnit.SECONDS)
            assertNotNull("HTTPS WebSocket handshake did not reach the test server", request)
            assertEquals("/socket", request!!.path)
            assertFalse(request.path.orEmpty().contains(ACCESS_TOKEN))
            assertEquals(
                buildMediaBrowserAuthorizationHeader(ACCESS_TOKEN, DEVICE_ID),
                request.getHeader("Authorization"),
            )
        } finally {
            client.stop()
            server.shutdown()
        }
    }

    private fun tlsFixtures(): TlsFixtures {
        val certificate = HeldCertificate.Builder()
            .addSubjectAlternativeName(TEST_HOST)
            .build()
        return TlsFixtures(
            server = HandshakeCertificates.Builder().heldCertificate(certificate).build(),
            client = HandshakeCertificates.Builder()
                .addTrustedCertificate(certificate.certificate)
                .build(),
        )
    }

    private data class TlsFixtures(
        val server: HandshakeCertificates,
        val client: HandshakeCertificates,
    )

    private class TestSessionRepository(
        private val serverUrl: String,
    ) : SessionRepository {
        override fun getAccessToken() = flowOf(ACCESS_TOKEN)
        override fun getDeviceId() = flowOf(DEVICE_ID)
        override fun getBaseUrl() = flowOf(serverUrl)
        override fun getCurrentUserId() = flowOf("user-1")
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private companion object {
        const val TEST_HOST = "syncplay-test.local"
        const val ACCESS_TOKEN = "syncplay-access-token"
        const val DEVICE_ID = "syncplay-device"
    }
}
