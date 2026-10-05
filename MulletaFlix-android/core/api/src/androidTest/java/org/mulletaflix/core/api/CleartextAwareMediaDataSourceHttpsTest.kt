package org.mulletaflix.core.api

import android.net.Uri
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DataSpec
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.net.InetAddress
import okhttp3.Dns
import okhttp3.OkHttpClient
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy

@UnstableApi
@RunWith(AndroidJUnit4::class)
class CleartextAwareMediaDataSourceHttpsTest {
    private lateinit var server: MockWebServer

    @Before
    fun setUp() {
        server = MockWebServer()
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    @Test
    fun media3DownloadsRemoteHttpsThroughTheCleartextPolicy() {
        val certificate = HeldCertificate.Builder()
            .addSubjectAlternativeName("media.example.org")
            .build()
        val serverTls = HandshakeCertificates.Builder()
            .heldCertificate(certificate)
            .build()
        val clientTls = HandshakeCertificates.Builder()
            .addTrustedCertificate(certificate.certificate)
            .build()
        server.useHttps(serverTls.sslSocketFactory(), false)
        server.enqueue(MockResponse().setBody("movie"))
        server.start(InetAddress.getByName("127.0.0.1"), 0)

        val client = OkHttpClient.Builder()
            .sslSocketFactory(clientTls.sslSocketFactory(), clientTls.trustManager)
            .dns(object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    listOf(InetAddress.getByName("127.0.0.1"))
            })
            .enforceLocalNetworkCleartextPolicy()
            .build()
        val remoteUrl = server.url("/Videos/item/stream")
            .newBuilder()
            .host("media.example.org")
            .build()
        val source = cleartextAwareMediaDataSourceFactory(client).createDataSource()
        val output = ByteArray(5)

        try {
            assertEquals(5L, source.open(DataSpec.Builder().setUri(Uri.parse(remoteUrl.toString())).build()))
            assertEquals(5, source.read(output, 0, output.size))
        } finally {
            source.close()
        }

        assertEquals("movie", output.decodeToString())
        assertEquals(1, server.requestCount)
    }
}
