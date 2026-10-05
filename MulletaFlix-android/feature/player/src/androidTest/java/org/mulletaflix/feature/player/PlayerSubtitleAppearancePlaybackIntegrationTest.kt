package org.mulletaflix.feature.player

import android.content.Context
import android.graphics.Bitmap
import android.graphics.Canvas
import android.net.Uri
import androidx.activity.ComponentActivity
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.ByteArrayOutputStream
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.subtitle.SUBTITLE_OUTLINE_COLOR
import org.mulletaflix.designsystem.subtitle.subtitleForegroundColor
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_50
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_80
import org.mulletaflix.domain.model.SUBTITLE_COLOR_CYAN

@RunWith(AndroidJUnit4::class)
@UnstableApi
class PlayerSubtitleAppearancePlaybackIntegrationTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun changingAppearanceFromTrackMenuRestylesCueDuringPlayback() {
        val server = MockWebServer()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse = when (request.path) {
                "/silence.wav" -> MockResponse()
                    .setHeader("Content-Type", "audio/wav")
                    .setBody(okio.Buffer().write(wavSilence()))
                "/subtitles/active.srt" -> MockResponse()
                    .setHeader("Content-Type", "application/x-subrip; charset=utf-8")
                    .setBody(SRT_CUE)
                else -> MockResponse().setResponseCode(404)
            }
        }
        server.start()

        val context = ApplicationProvider.getApplicationContext<Context>()
        val playbackPlayer = ExoPlayer.Builder(context).build()
        val subtitleViewRef = AtomicReference<androidx.media3.ui.SubtitleView?>()
        val styleListener = AtomicReference<Player.Listener?>()
        val cueDisplayed = CountDownLatch(1)
        val playbackError = AtomicReference<androidx.media3.common.PlaybackException?>()
        playbackPlayer.addListener(object : Player.Listener {
            override fun onCues(cueGroup: androidx.media3.common.text.CueGroup) {
                if (cueGroup.cues.any { it.text?.toString() == EXPECTED_CUE }) cueDisplayed.countDown()
            }

            override fun onPlayerError(error: androidx.media3.common.PlaybackException) {
                playbackError.set(error)
            }
        })

        try {
            val subtitle = MediaItem.SubtitleConfiguration.Builder(
                Uri.parse(server.url("/subtitles/active.srt").toString()),
            )
                .setMimeType(MimeTypes.APPLICATION_SUBRIP)
                .setLanguage("pt-BR")
                .setLabel("Português")
                .setSelectionFlags(C.SELECTION_FLAG_DEFAULT)
                .build()
            val mediaItem = MediaItem.Builder()
                .setUri(server.url("/silence.wav").toString())
                .setSubtitleConfigurations(listOf(subtitle))
                .build()

            composeRule.setContent {
                var state by remember {
                    mutableStateOf(
                        PlayerState(
                            subtitleFontSize = 100,
                            subtitleBackground = SUBTITLE_BACKGROUND_BLACK_50,
                        ),
                    )
                }
                var showTrackMenu by remember { mutableStateOf(true) }
                var showAppearanceMenu by remember { mutableStateOf(false) }

                Column(Modifier.fillMaxSize()) {
                    AndroidView(
                        modifier = Modifier.fillMaxSize().weight(1f).padding(bottom = 8.dp),
                        factory = { viewContext ->
                            PlayerView(viewContext).apply {
                                useController = false
                                player = playbackPlayer
                                subtitleView?.let { activeSubtitleView ->
                                    subtitleViewRef.set(activeSubtitleView)
                                    val listener = userSubtitleCueStyleListener(activeSubtitleView)
                                    styleListener.set(listener)
                                    playbackPlayer.addListener(listener)
                                    listener.onCues(playbackPlayer.currentCues)
                                }
                            }
                        },
                        update = { playerView ->
                            playerView.subtitleView?.let { activeSubtitleView ->
                                applyUserSubtitlePreferences(
                                    subtitleView = activeSubtitleView,
                                    fontSizePercent = state.subtitleFontSize,
                                    style = subtitleCaptionStyle(
                                        foregroundColor = subtitleForegroundColor(state.subtitleColor).toArgb(),
                                        edgeColor = SUBTITLE_OUTLINE_COLOR.toArgb(),
                                        backgroundCode = state.subtitleBackground,
                                    ),
                                )
                            }
                        },
                        onRelease = { playerView -> playerView.player = null },
                    )
                    if (showTrackMenu) {
                        PlayerTrackMenu(
                            title = "Legendas",
                            tracks = listOf(TrackInfo(index = 14, displayName = "Português")),
                            selectedIndex = 0,
                            onSelect = {},
                            onDismiss = { showTrackMenu = false },
                            onAppearanceClick = {
                                showTrackMenu = false
                                showAppearanceMenu = true
                            },
                        )
                    }
                    if (showAppearanceMenu) {
                        PlayerSubtitleAppearanceMenu(
                            state = state,
                            onSubtitleFontSizeSelect = { state = state.copy(subtitleFontSize = it) },
                            onSubtitleColorSelect = { state = state.copy(subtitleColor = it) },
                            onSubtitleBackgroundSelect = { state = state.copy(subtitleBackground = it) },
                            onDismiss = { showAppearanceMenu = false },
                        )
                    }
                }
            }
            composeRule.waitUntil(timeoutMillis = 5_000) { (subtitleViewRef.get()?.width ?: 0) > 0 }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playbackPlayer.setMediaItem(mediaItem)
                playbackPlayer.prepare()
                playbackPlayer.playWhenReady = true
            }

            assertTrue("Media3 did not render the active SRT cue", cueDisplayed.await(10, TimeUnit.SECONDS))
            composeRule.waitUntil(timeoutMillis = 5_000) {
                countBlackBacking(captureSubtitlePixels(subtitleViewRef), 0x80) > 0
            }
            assertEquals(null, playbackError.get())

            composeRule.onNodeWithText("Personalizar aparência").performClick()
            composeRule.onNodeWithText("200%").performClick()
            val cyan = composeRule.onNodeWithText("Ciano")
            if (isCompactWidth()) cyan.performScrollTo()
            cyan.performClick()
            val black80 = composeRule.onNodeWithText("Preto 80%")
            if (isCompactWidth()) black80.performScrollTo()
            black80.performClick()

            composeRule.waitUntil(timeoutMillis = 5_000) {
                countBlackBacking(captureSubtitlePixels(subtitleViewRef), 0xCC) > 0
            }
            assertEquals(null, playbackError.get())
            assertTrue("Playback should continue while the appearance menu is used", isPlaying(playbackPlayer))
            composeRule.onNodeWithText("200%").assertSelectedSubtitleOption()
            composeRule.onNodeWithText("Ciano").assertSelectedSubtitleOption()
            composeRule.onNodeWithText("Preto 80%").assertSelectedSubtitleOption()
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                styleListener.getAndSet(null)?.let(playbackPlayer::removeListener)
                playbackPlayer.release()
            }
            server.shutdown()
        }
    }

    private fun captureSubtitlePixels(subtitleView: AtomicReference<androidx.media3.ui.SubtitleView?>): IntArray {
        val captured = AtomicReference<IntArray>()
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            val view = checkNotNull(subtitleView.get())
            val bitmap = Bitmap.createBitmap(view.width, view.height, Bitmap.Config.ARGB_8888)
            try {
                view.draw(Canvas(bitmap))
                val pixels = IntArray(bitmap.width * bitmap.height)
                bitmap.getPixels(pixels, 0, bitmap.width, 0, 0, bitmap.width, bitmap.height)
                captured.set(pixels)
            } finally {
                bitmap.recycle()
            }
        }
        return checkNotNull(captured.get())
    }

    private fun countBlackBacking(pixels: IntArray, alpha: Int): Int = pixels.count {
        android.graphics.Color.alpha(it) == alpha &&
            android.graphics.Color.red(it) == 0 &&
            android.graphics.Color.green(it) == 0 &&
            android.graphics.Color.blue(it) == 0
    }

    private fun isPlaying(player: ExoPlayer): Boolean {
        val result = AtomicReference(false)
        InstrumentationRegistry.getInstrumentation().runOnMainSync { result.set(player.isPlaying) }
        return result.get()
    }

    private fun androidx.compose.ui.test.SemanticsNodeInteraction.assertSelectedSubtitleOption() {
        assertTrue("Subtitle option should be selected", fetchSemanticsNode().config.contains(androidx.compose.ui.semantics.SemanticsProperties.Selected))
    }

    private fun isCompactWidth(): Boolean =
        InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration.screenWidthDp < 600

    private fun wavSilence(): ByteArray {
        val sampleRate = 8_000
        val sampleCount = sampleRate * 20
        val dataSize = sampleCount * 2
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
        const val EXPECTED_CUE = "Legenda ativa durante a reprodução"
        val SRT_CUE = """
            1
            00:00:00,500 --> 00:00:15,500
            <i><font color="#ff0000">$EXPECTED_CUE</font></i>

        """.trimIndent()
    }
}
