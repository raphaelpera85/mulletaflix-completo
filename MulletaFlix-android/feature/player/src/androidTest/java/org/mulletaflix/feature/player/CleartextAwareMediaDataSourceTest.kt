package org.mulletaflix.feature.player

import android.net.Uri
import androidx.media3.common.C
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DataSpec
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.io.IOException
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.cleartextAwareMediaDataSourceFactory
import org.mulletaflix.core.common.network.CleartextTrafficPolicy

@UnstableApi
@RunWith(AndroidJUnit4::class)
class CleartextAwareMediaDataSourceTest {
    private lateinit var server: MockWebServer

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    @Test
    fun media3RefusesPublicHttpStreamBeforeSendingUrlToken() {
        val source = factory().createDataSource()
        val failure = runCatching {
            source.open(dataSpec("http://203.0.113.24:8096/Videos/item/stream?api_key=session-token"))
        }.exceptionOrNull()

        assertTrue("expected cleartext denial, received $failure", failure is IOException)
        assertTrue("expected cleartext policy failure, received $failure", hasPolicyFailure(failure))
    }

    @Test
    fun media3StreamsOverHttpFromLocalServer() {
        server.enqueue(MockResponse().setBody("movie"))
        val source = factory().createDataSource()
        val output = ByteArray(5)

        try {
            assertEquals(5L, source.open(dataSpec(server.url("/Videos/item/stream").toString())))
            assertEquals(5, source.read(output, 0, output.size))
        } finally {
            source.close()
        }

        assertEquals("movie", output.decodeToString())
        assertEquals(1, server.requestCount)
    }

    @Test
    fun media3FollowsRedirectToAnotherLocalLanPath() {
        server.enqueue(MockResponse().setResponseCode(302).addHeader("Location", "/Videos/item/final"))
        server.enqueue(MockResponse().setBody("movie"))
        val source = factory().createDataSource()
        val output = ByteArray(5)

        try {
            assertEquals(5L, source.open(dataSpec(server.url("/Videos/item/stream").toString())))
            assertEquals(5, source.read(output, 0, output.size))
        } finally {
            source.close()
        }

        assertEquals("movie", output.decodeToString())
        assertEquals(2, server.requestCount)
        assertEquals("/Videos/item/stream", server.takeRequest().requestUrl?.encodedPath)
        assertEquals("/Videos/item/final", server.takeRequest().requestUrl?.encodedPath)
    }

    @Test
    fun media3BlocksPublicHttpRedirectFromLanStream() {
        server.enqueue(
            MockResponse()
                .setResponseCode(302)
                .addHeader("Location", "http://203.0.113.25:8096/external?api_key=session-token"),
        )
        val source = factory().createDataSource()
        val failure = try {
            runCatching { source.open(dataSpec(server.url("/Videos/item/stream").toString())) }.exceptionOrNull()
        } finally {
            source.close()
        }

        assertTrue("expected redirected cleartext denial, received $failure", failure is IOException)
        assertTrue("expected redirect policy failure, received $failure", hasPolicyFailure(failure))
        assertEquals("only the initial LAN request reaches the server", 1, server.requestCount)
    }

    @Test
    fun httpsRemoteMediaRemainsAllowedByPolicy() {
        assertTrue(CleartextTrafficPolicy.isAllowed("https://media.example.org/Videos/item/stream"))
    }

    private fun factory() = cleartextAwareMediaDataSourceFactory(
        connectTimeoutMs = MEDIA_CONNECT_TIMEOUT_MS,
        readTimeoutMs = MEDIA_READ_TIMEOUT_MS,
    )

    private fun dataSpec(url: String) = DataSpec.Builder()
        .setUri(Uri.parse(url))
        .build()

    private fun hasPolicyFailure(failure: Throwable?): Boolean =
        generateSequence(failure) { it.cause }
            .any { it.message == CleartextTrafficPolicy.BLOCKED_MESSAGE }
}
