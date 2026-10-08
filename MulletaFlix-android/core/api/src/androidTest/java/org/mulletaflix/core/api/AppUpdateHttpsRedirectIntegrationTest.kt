package org.mulletaflix.core.api

import androidx.test.ext.junit.runners.AndroidJUnit4
import java.io.IOException
import java.net.InetAddress
import okhttp3.Dns
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy

@RunWith(AndroidJUnit4::class)
class AppUpdateHttpsRedirectIntegrationTest {
    @Test
    fun httpsUpdateRedirectToLocalHttpIsBlockedBeforeTheTargetRequest() {
        val fixtures = tlsFixtures()
        val localHttpTarget = MockWebServer().apply { start(InetAddress.getByName("127.0.0.1"), 0) }
        val updateServer = httpsServer(fixtures.serverCertificates).apply {
            enqueue(
                MockResponse().setResponseCode(302)
                    .setHeader("Location", localHttpTarget.url("/apk").toString()),
            )
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val client = updateClient(fixtures.clientCertificates)

        try {
            val failure = runCatching {
                client.newCall(Request.Builder().url(updateUrl(updateServer, UPDATE_HOST)).build())
                    .execute().use { }
            }.exceptionOrNull()

            assertTrue("Expected HTTPS downgrade rejection, got $failure", failure is IOException)
            assertTrue(
                "Expected the updater-specific policy error, got $failure",
                generateSequence(failure) { it.cause }
                    .any { it.message == "HTTPS requests must not redirect to HTTP." },
            )
            assertEquals("Only the original HTTPS request may reach its server", 1, updateServer.requestCount)
            assertEquals("HTTP target must receive no request", 0, localHttpTarget.requestCount)
        } finally {
            updateServer.shutdown()
            localHttpTarget.shutdown()
        }
    }

    @Test
    fun httpsUpdateRedirectToHttpsCdnRemainsSupported() {
        val fixtures = tlsFixtures()
        val cdnServer = httpsServer(fixtures.serverCertificates).apply {
            enqueue(MockResponse().setBody("apk-payload"))
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val updateServer = httpsServer(fixtures.serverCertificates).apply {
            enqueue(
                MockResponse().setResponseCode(302)
                    .setHeader("Location", cdnServer.url("/asset.apk").newBuilder().host(CDN_HOST).build()),
            )
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val client = updateClient(fixtures.clientCertificates)

        try {
            client.newCall(Request.Builder().url(updateUrl(updateServer, UPDATE_HOST)).build())
                .execute().use { response ->
                    assertTrue("HTTPS CDN redirect should complete", response.isSuccessful)
                    assertEquals("apk-payload", response.body?.string())
                }

            assertEquals(1, updateServer.requestCount)
            assertEquals(1, cdnServer.requestCount)
            assertEquals("/asset.apk", cdnServer.takeRequest().path)
        } finally {
            updateServer.shutdown()
            cdnServer.shutdown()
        }
    }

    @Test
    fun crossOriginHttpsRedirectStripsCredentialsAndKeepsDestinationParameters() {
        val fixtures = tlsFixtures()
        val cdnServer = httpsServer(fixtures.serverCertificates).apply {
            enqueue(MockResponse().setBody("apk-payload"))
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val redirectUrl = cdnServer.url("/asset.apk").newBuilder()
            .host(CDN_HOST)
            .username("embedded-user")
            .password("embedded-password")
            .addQueryParameter("cursor", "page-2")
            .addQueryParameter("signature", "destination-signature")
            .addQueryParameter("access_token", "must-not-leak")
            .build()
        val updateServer = httpsServer(fixtures.serverCertificates).apply {
            enqueue(MockResponse().setResponseCode(302).setHeader("Location", redirectUrl))
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val observedUrls = mutableListOf<okhttp3.HttpUrl>()
        val client = updateClient(fixtures.clientCertificates).newBuilder()
            .addInterceptor { chain ->
                observedUrls += chain.request().url
                chain.proceed(chain.request())
            }
            .build()
        val request = Request.Builder()
            .url(updateUrl(updateServer, UPDATE_HOST))
            .header("Authorization", "Bearer source-token")
            .header("Cookie", "session=source-cookie")
            .header("Proxy-Authorization", "Basic source-proxy-token")
            .build()

        try {
            client.newCall(request).execute().use { response ->
                assertTrue("HTTPS CDN redirect should complete", response.isSuccessful)
                assertEquals("apk-payload", response.body?.string())
            }

            val cdnRequest = cdnServer.takeRequest()
            assertEquals(null, cdnRequest.getHeader("Authorization"))
            assertEquals(null, cdnRequest.getHeader("Cookie"))
            assertEquals(null, cdnRequest.getHeader("Proxy-Authorization"))
            assertEquals("Both requests should reach the policy chain", 2, observedUrls.size)
            assertEquals("", observedUrls[1].username)
            assertEquals("", observedUrls[1].password)
            assertEquals("page-2", cdnRequest.requestUrl?.queryParameter("cursor"))
            assertEquals("destination-signature", cdnRequest.requestUrl?.queryParameter("signature"))
            assertEquals(null, cdnRequest.requestUrl?.queryParameter("access_token"))
            assertEquals(1, updateServer.requestCount)
            assertEquals(1, cdnServer.requestCount)
        } finally {
            updateServer.shutdown()
            cdnServer.shutdown()
        }
    }

    @Test
    fun updateRedirectChainStopsAtTwentyRedirects() {
        val fixtures = tlsFixtures()
        val updateServer = httpsServer(fixtures.serverCertificates).apply {
            repeat(21) {
                enqueue(MockResponse().setResponseCode(302).setHeader("Location", "/loop"))
            }
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val client = updateClient(fixtures.clientCertificates)

        try {
            val failure = runCatching {
                client.newCall(Request.Builder().url(updateUrl(updateServer, UPDATE_HOST)).build())
                    .execute().use { }
            }.exceptionOrNull()

            assertTrue("Expected redirect-limit rejection, got $failure", failure is IOException)
            assertTrue(
                "Expected the redirect-limit error, got $failure",
                generateSequence(failure) { it.cause }
                    .any { it.message == "Too many redirects: 20" },
            )
            assertEquals("The initial request plus 20 redirects may reach the server", 21, updateServer.requestCount)
        } finally {
            updateServer.shutdown()
        }
    }

    private fun tlsFixtures(): TlsFixtures {
        val certificate = HeldCertificate.Builder()
            .addSubjectAlternativeName(UPDATE_HOST)
            .addSubjectAlternativeName(CDN_HOST)
            .build()
        return TlsFixtures(
            serverCertificates = HandshakeCertificates.Builder().heldCertificate(certificate).build(),
            clientCertificates = HandshakeCertificates.Builder()
                .addTrustedCertificate(certificate.certificate)
                .build(),
        )
    }

    private fun httpsServer(certificates: HandshakeCertificates) = MockWebServer().apply {
        useHttps(certificates.sslSocketFactory(), false)
    }

    private fun updateClient(certificates: HandshakeCertificates) = OkHttpClient.Builder()
        .sslSocketFactory(certificates.sslSocketFactory(), certificates.trustManager)
        .dns(object : Dns {
            override fun lookup(hostname: String): List<InetAddress> =
                if (hostname in setOf(UPDATE_HOST, CDN_HOST)) listOf(InetAddress.getByName("127.0.0.1"))
                else Dns.SYSTEM.lookup(hostname)
        })
        .enforceLocalNetworkCleartextPolicy(requireHttpsRedirects = true)
        .build()

    private fun updateUrl(server: MockWebServer, host: String) =
        server.url("/releases/app.apk").newBuilder().host(host).build()

    private data class TlsFixtures(
        val serverCertificates: HandshakeCertificates,
        val clientCertificates: HandshakeCertificates,
    )

    private companion object {
        const val UPDATE_HOST = "updates.example.test"
        const val CDN_HOST = "cdn.example.test"
    }
}
