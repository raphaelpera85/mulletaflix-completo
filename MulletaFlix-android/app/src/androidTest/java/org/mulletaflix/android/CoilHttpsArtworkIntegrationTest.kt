package org.mulletaflix.android

import android.graphics.Bitmap
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import coil.ImageLoader
import coil.request.CachePolicy
import coil.request.ErrorResult
import coil.request.ImageRequest
import coil.request.SuccessResult
import java.io.ByteArrayOutputStream
import java.net.InetAddress
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import okhttp3.Dns
import okhttp3.Interceptor
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.buildAuthenticatedImageClient
import org.mulletaflix.core.common.network.CleartextTrafficPolicy

@RunWith(AndroidJUnit4::class)
class CoilHttpsArtworkIntegrationTest {
    private lateinit var server: MockWebServer
    private lateinit var imageLoader: ImageLoader
    private val context = InstrumentationRegistry.getInstrumentation().targetContext
    private val imageBytes by lazy(::createPngFixture)

    @Before
    fun setUp() {
        val certificate = HeldCertificate.Builder()
            .addSubjectAlternativeName(HOST)
            .build()
        val serverTls = HandshakeCertificates.Builder()
            .heldCertificate(certificate)
            .build()
        val clientTls = HandshakeCertificates.Builder()
            .addTrustedCertificate(certificate.certificate)
            .build()

        server = MockWebServer().apply {
            useHttps(serverTls.sslSocketFactory(), false)
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse = when (request.path?.substringBefore('?')) {
                    "/artwork.png" -> MockResponse()
                        .setHeader("Content-Type", "image/png")
                        .setBody(okio.Buffer().write(imageBytes))
                    "/artwork-redirect.png" -> MockResponse()
                        .setResponseCode(302)
                        .setHeader("Location", "http://203.0.113.77:8096/artwork.png")
                    else -> MockResponse().setResponseCode(404)
                }
            }
            start(InetAddress.getByName("127.0.0.1"), 0)
        }

        val sessionRepository = object : SessionRepository {
            override fun getAccessToken() = flowOf("artwork-session-token")
            override fun getDeviceId() = flowOf("coil-test-device")
            override fun getBaseUrl() = flowOf("https://$HOST")
            override fun getCurrentUserId() = flowOf("coil-test-user")
            override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
            override suspend fun setBaseUrl(url: String) = Unit
            override suspend fun clearSession() = Unit
        }
        val authenticatedClient = buildAuthenticatedImageClient(
            serverUrlInterceptor = Interceptor { chain -> chain.proceed(chain.request()) },
            clientIdentityInterceptor = ClientIdentityInterceptor(sessionRepository),
        )
        // The production factory owns redirects and identity; inject only the trusted local TLS fixture.
        val fixtureClient = authenticatedClient.newBuilder()
            .sslSocketFactory(clientTls.sslSocketFactory(), clientTls.trustManager)
            .dns(object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    if (hostname == HOST) listOf(InetAddress.getByName("127.0.0.1"))
                    else Dns.SYSTEM.lookup(hostname)
            })
            .build()
        imageLoader = ImageLoader.Builder(context)
            .okHttpClient(fixtureClient)
            .build()
    }

    @After
    fun tearDown() {
        if (::imageLoader.isInitialized) imageLoader.shutdown()
        if (::server.isInitialized) server.shutdown()
    }

    @Test
    fun coilFetchesAndDecodesArtworkOverTrustedRemoteHttps() = runBlocking {
        val result = imageLoader.execute(request("/artwork.png"))

        assertTrue("Expected Coil to decode HTTPS artwork, got $result", result is SuccessResult)
        assertEquals(2, (result as SuccessResult).drawable.intrinsicWidth)
        assertEquals(3, result.drawable.intrinsicHeight)

        val request = server.takeRequest()
        assertEquals("/artwork.png", request.path?.substringBefore('?'))
        assertTrue(request.getHeader("Authorization").orEmpty().contains("Token=\"artwork-session-token\""))
        assertEquals(1, server.requestCount)
    }

    @Test
    fun coilBlocksHttpsArtworkRedirectToPublicHttpBeforeASecondRequest() = runBlocking {
        val result = imageLoader.execute(request("/artwork-redirect.png"))

        assertTrue("Expected Coil to reject public cleartext redirect, got $result", result is ErrorResult)
        val failure = (result as ErrorResult).throwable
        assertTrue(
            "Expected cleartext policy rejection in the cause chain, got $failure",
            generateSequence(failure) { it.cause }
                .any { it.message == CleartextTrafficPolicy.BLOCKED_MESSAGE },
        )
        assertEquals("Only the original HTTPS artwork request may reach the server", 1, server.requestCount)
        assertEquals("/artwork-redirect.png", server.takeRequest().path?.substringBefore('?'))
    }

    private fun request(path: String): ImageRequest = ImageRequest.Builder(context)
        .data(server.url(path).newBuilder().host(HOST).build().toString())
        .memoryCachePolicy(CachePolicy.DISABLED)
        .diskCachePolicy(CachePolicy.DISABLED)
        .build()

    private fun createPngFixture(): ByteArray {
        val bitmap = Bitmap.createBitmap(2, 3, Bitmap.Config.ARGB_8888)
        return try {
            ByteArrayOutputStream().use { output ->
                check(bitmap.compress(Bitmap.CompressFormat.PNG, 100, output))
                output.toByteArray()
            }
        } finally {
            bitmap.recycle()
        }
    }

    private companion object {
        const val HOST = "media.example.org"
    }
}
