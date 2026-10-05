package org.mulletaflix.core.api

import android.content.Context
import android.net.Uri
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.C
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.text.CueGroup
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.ByteArrayOutputStream
import java.net.InetAddress
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import okhttp3.Dns
import okhttp3.OkHttpClient
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy

@UnstableApi
@RunWith(AndroidJUnit4::class)
class ExternalSubtitleHttpsPlaybackIntegrationTest {
    @Test
    fun externalSubtitleIsFetchedAndDecodedOverRemoteHttpsThroughMedia3PolicyFactory() {
        val certificate = HeldCertificate.Builder()
            .addSubjectAlternativeName(HOST)
            .build()
        val serverTls = HandshakeCertificates.Builder()
            .heldCertificate(certificate)
            .build()
        val clientTls = HandshakeCertificates.Builder()
            .addTrustedCertificate(certificate.certificate)
            .build()
        val server = MockWebServer().apply {
            useHttps(serverTls.sslSocketFactory(), false)
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse = when (request.path) {
                    "/Videos/item/stream" -> MockResponse()
                        .setHeader("Content-Type", "audio/wav")
                        .setBody(okio.Buffer().write(wavSilence()))
                    "/subtitles/14.srt" -> MockResponse()
                        .setHeader("Content-Type", "application/x-subrip; charset=utf-8")
                        .setBody(SRT_CUE)
                    else -> MockResponse().setResponseCode(404)
                }
            }
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val client = OkHttpClient.Builder()
            .sslSocketFactory(clientTls.sslSocketFactory(), clientTls.trustManager)
            .dns(object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    if (hostname == HOST) listOf(InetAddress.getByName("127.0.0.1"))
                    else Dns.SYSTEM.lookup(hostname)
            })
            .enforceLocalNetworkCleartextPolicy()
            .build()
        val subtitleDecoded = CountDownLatch(1)
        val playbackFailure = AtomicReference<PlaybackException?>()
        val playerReference = AtomicReference<ExoPlayer?>()
        val context = InstrumentationRegistry.getInstrumentation().targetContext

        try {
            val item = MediaItem.Builder()
                .setUri(server.url("/Videos/item/stream").newBuilder().host(HOST).build().toString())
                .setSubtitleConfigurations(
                    listOf(
                        MediaItem.SubtitleConfiguration.Builder(
                            Uri.parse(server.url("/subtitles/14.srt").newBuilder().host(HOST).build().toString()),
                        )
                            .setMimeType(MimeTypes.APPLICATION_SUBRIP)
                            .setLanguage("pt-BR")
                            .setLabel("Português")
                            .setSelectionFlags(C.SELECTION_FLAG_DEFAULT)
                            .build(),
                    ),
                )
                .build()

            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                val player = ExoPlayer.Builder(context)
                    .setMediaSourceFactory(
                        DefaultMediaSourceFactory(
                            cleartextAwareMediaDataSourceFactory(client),
                        ),
                    )
                    .build()
                playerReference.set(player)
                player.addListener(object : Player.Listener {
                    override fun onCues(cueGroup: CueGroup) {
                        if (cueGroup.cues.any { it.text?.toString() == EXPECTED_CUE }) {
                            subtitleDecoded.countDown()
                        }
                    }

                    override fun onPlayerError(error: PlaybackException) {
                        playbackFailure.set(error)
                    }
                })
                player.setMediaItem(item)
                player.prepare()
                player.playWhenReady = true
            }

            assertTrue(
                "Media3 did not decode HTTPS sidecar subtitle; error=${playbackFailure.get()?.message}",
                subtitleDecoded.await(15, TimeUnit.SECONDS),
            )
            assertNull("HTTPS playback failed", playbackFailure.get())

            val requestPaths = (0 until server.requestCount).mapNotNull {
                server.takeRequest(1, TimeUnit.SECONDS)?.path
            }
            assertTrue("media request missing from HTTPS fixture: $requestPaths", "/Videos/item/stream" in requestPaths)
            assertEquals("subtitle must be fetched once: $requestPaths", 1, requestPaths.count { it == "/subtitles/14.srt" })
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerReference.getAndSet(null)?.release()
            }
            server.shutdown()
        }
    }

    private fun wavSilence(): ByteArray {
        val sampleRate = 8_000
        val dataSize = sampleRate * 20 * 2
        val output = ByteArrayOutputStream(44 + dataSize)
        val header = ByteBuffer.allocate(44).order(ByteOrder.LITTLE_ENDIAN)
        header.put("RIFF".toByteArray(Charsets.US_ASCII))
        header.putInt(36 + dataSize)
        header.put("WAVE".toByteArray(Charsets.US_ASCII))
        header.put("fmt ".toByteArray(Charsets.US_ASCII))
        header.putInt(16)
        header.putShort(1)
        header.putShort(1)
        header.putInt(sampleRate)
        header.putInt(sampleRate * 2)
        header.putShort(2)
        header.putShort(16)
        header.put("data".toByteArray(Charsets.US_ASCII))
        header.putInt(dataSize)
        output.write(header.array())
        output.write(ByteArray(dataSize))
        return output.toByteArray()
    }

    private companion object {
        const val HOST = "media.example.org"
        const val EXPECTED_CUE = "Legenda HTTPS integrada"
        val SRT_CUE = """
            1
            00:00:00,500 --> 00:00:15,500
            $EXPECTED_CUE

        """.trimIndent()
    }
}
