package org.mulletaflix.feature.player

import android.graphics.Bitmap
import android.graphics.Canvas
import androidx.activity.ComponentActivity
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.viewinterop.AndroidView
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import android.content.Context

@RunWith(AndroidJUnit4::class)
class ExternalSubtitlePlaybackIntegrationTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun serverSidecarIsRequestedAndDecodedIntoPlaybackCues() {
        val server = MockWebServer()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse = when (request.path) {
                "/silence.wav" -> MockResponse()
                    .setHeader("Content-Type", "audio/wav")
                    .setBody(okio.Buffer().write(wavSilence()))
                "/subtitles/14.srt" -> MockResponse()
                    .setHeader("Content-Type", "application/x-subrip; charset=utf-8")
                    .setBody(SRT_CUE)
                else -> MockResponse().setResponseCode(404)
            }
        }
        server.start()

        val cueDecoded = CountDownLatch(1)
        val playbackFailed = AtomicReference<androidx.media3.common.PlaybackException?>()
        val subtitleRequested = AtomicBoolean(false)
        val playerRef = AtomicReference<ExoPlayer?>()
        val subtitleViewRef = AtomicReference<androidx.media3.ui.SubtitleView?>()
        val context = ApplicationProvider.getApplicationContext<Context>()

        try {
            val subtitleUrl = server.url("/subtitles/14.srt").toString()
            val mediaUrl = server.url("/silence.wav").toString()
            val subtitle = buildExternalSubtitleConfiguration(
                serverIndex = 14,
                subtitleUrl = subtitleUrl,
                mimeType = MimeTypes.APPLICATION_SUBRIP,
                language = "pt-BR",
                label = "Português (Brasil)",
                isDefault = true,
                isForced = false,
            )
            val item = MediaItem.Builder()
                .setUri(mediaUrl)
                .setSubtitleConfigurations(listOf(subtitle))
                .build()

            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerRef.set(ExoPlayer.Builder(context).build().apply {
                    addListener(object : Player.Listener {
                        override fun onCues(cueGroup: androidx.media3.common.text.CueGroup) {
                            if (cueGroup.cues.any { it.text?.toString() == EXPECTED_CUE }) {
                                cueDecoded.countDown()
                            }
                        }

                        override fun onPlayerError(error: androidx.media3.common.PlaybackException) {
                            playbackFailed.set(error)
                        }
                    })
                })
            }
            composeRule.setContent {
                Box(Modifier.fillMaxSize().background(Color.Black)) {
                    AndroidView(
                        modifier = Modifier.fillMaxSize(),
                        factory = { context ->
                            PlayerView(context).apply {
                                useController = false
                                player = playerRef.get()
                                subtitleViewRef.set(subtitleView)
                            }
                        },
                    )
                }
            }
            composeRule.waitForIdle()
            composeRule.waitUntil(5_000) { (subtitleViewRef.get()?.width ?: 0) > 0 }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerRef.get()?.apply {
                    setMediaItem(item)
                    prepare()
                    playWhenReady = true
                }
            }

            val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10)
            while (System.nanoTime() < deadline && !subtitleRequested.get()) {
                val request = server.takeRequest(200, TimeUnit.MILLISECONDS) ?: continue
                if (request.path == "/subtitles/14.srt") subtitleRequested.set(true)
            }

            assertTrue(
                "Playback failed before requesting the subtitle: ${playbackFailed.get()?.message}",
                playbackFailed.get() == null,
            )
            assertTrue("Media3 did not request the external subtitle URL", subtitleRequested.get())
            assertTrue(
                "Media3 did not emit the SRT cue; player error=${playbackFailed.get()?.message}",
                cueDecoded.await(10, TimeUnit.SECONDS),
            )
            composeRule.waitForIdle()
            val renderedPixels = AtomicInteger(0)
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                val subtitleView = checkNotNull(subtitleViewRef.get())
                val bitmap = Bitmap.createBitmap(subtitleView.width, subtitleView.height, Bitmap.Config.ARGB_8888)
                subtitleView.draw(Canvas(bitmap))
                val pixels = IntArray(bitmap.width * bitmap.height)
                bitmap.getPixels(pixels, 0, bitmap.width, 0, 0, bitmap.width, bitmap.height)
                renderedPixels.set(pixels.count { android.graphics.Color.alpha(it) > 0 })
                bitmap.recycle()
            }
            assertTrue(
                "PlayerView SubtitleView did not render the decoded cue",
                renderedPixels.get() > 0,
            )
            assertEquals(null, playbackFailed.get())
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerRef.getAndSet(null)?.release()
            }
            server.shutdown()
        }
    }

    private fun wavSilence(): ByteArray {
        val sampleRate = 8_000
        val sampleCount = sampleRate * 6
        val dataSize = sampleCount * 2
        val output = java.io.ByteArrayOutputStream(44 + dataSize)
        val header = java.nio.ByteBuffer.allocate(44).order(java.nio.ByteOrder.LITTLE_ENDIAN)
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
        const val EXPECTED_CUE = "Legenda externa real"
        val SRT_CUE = """
            1
            00:00:00,500 --> 00:00:04,500
            $EXPECTED_CUE

        """.trimIndent()
    }
}
