package org.mulletaflix.feature.itemdetail

import androidx.test.ext.junit.runners.AndroidJUnit4
import java.net.InetAddress
import kotlinx.coroutines.runBlocking
import okhttp3.Dns
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.readium.r2.shared.util.AbsoluteUrl
import org.readium.r2.shared.util.http.HttpRequest

@RunWith(AndroidJUnit4::class)
class BookReaderHttpPolicyIntegrationTest {
    @Test
    fun localHttpBookResourceStreamsThroughTheProtectedReadiumClient() = runBlocking {
        MockWebServer().use { server ->
            server.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "application/epub+zip")
                    .setBody("readium local resource"),
            )
            server.start()

            val client = createBookReaderHttpClient()
            val result = client.stream(HttpRequest(url = requireNotNull(AbsoluteUrl(server.url("/book.epub").toString()))))

            assertTrue("The protected client should permit a loopback HTTP resource", result.isSuccess)
            val stream = requireNotNull(result.getOrNull())
            assertEquals("application/epub+zip", stream.response.mediaType.toString())
            stream.body.use { body ->
                assertEquals("readium local resource", body.readBytes().decodeToString())
            }
            assertEquals(1, server.requestCount)
        }
    }

    @Test
    fun publicHttpBookResourceIsRejectedBeforeDnsOrNetworkRequest() = runBlocking {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setBody("must not be requested"))
            server.start()
            val localOnlyDns = object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    listOf(InetAddress.getByName("127.0.0.1"))
            }
            val publicUrl = server.url("/book.epub")
                .newBuilder()
                .host("public.example.test")
                .build()

            val result = createBookReaderHttpClient(localOnlyDns)
                .stream(HttpRequest(url = requireNotNull(AbsoluteUrl(publicUrl.toString()))))

            assertTrue("Public HTTP must fail under the cleartext policy", result.isFailure)
            assertNotNull(result.failureOrNull())
            assertEquals("The denied request must not reach the fixture", 0, server.requestCount)
        }
    }

    @Test
    fun localHttpRedirectToPublicHostIsRejectedBeforeTheTargetRequest() = runBlocking {
        MockWebServer().use { server ->
            server.start()
            val publicTarget = server.url("/external-book.epub")
                .newBuilder()
                .host("public.example.test")
                .build()
            server.enqueue(MockResponse().setResponseCode(302).setHeader("Location", publicTarget))
            server.enqueue(MockResponse().setBody("must not be requested"))
            val localOnlyDns = object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    listOf(InetAddress.getByName("127.0.0.1"))
            }

            val result = createBookReaderHttpClient(localOnlyDns)
                .stream(HttpRequest(url = requireNotNull(AbsoluteUrl(server.url("/redirect.epub").toString()))))

            assertTrue("A public HTTP redirect must be denied", result.isFailure)
            assertEquals("Only the local redirect response may reach the fixture", 1, server.requestCount)
        }
    }

    @Test
    fun sameOriginLocalHttpRedirectRemainsSupported() = runBlocking {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setResponseCode(302).setHeader("Location", "/final.epub"))
            server.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "application/epub+zip")
                    .setBody("redirected local book"),
            )
            server.start()

            val result = createBookReaderHttpClient()
                .stream(HttpRequest(url = requireNotNull(AbsoluteUrl(server.url("/redirect.epub").toString()))))

            assertTrue("A same-origin local redirect should still open", result.isSuccess)
            requireNotNull(result.getOrNull()).body.use { body ->
                assertEquals("redirected local book", body.readBytes().decodeToString())
            }
            assertEquals(2, server.requestCount)
        }
    }
}
