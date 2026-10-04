package org.mulletaflix.feature.player

import android.content.pm.PackageManager
import android.net.Uri
import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.lifecycle.Lifecycle
import androidx.media3.common.MediaItem
import androidx.media3.exoplayer.ExoPlayer
import androidx.test.core.app.ApplicationProvider
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

class PlayerBackgroundPlaybackTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun activeMediaKeepsPlayingOnPauseThenPausesWhenActivityStops() = verifyPlayRequestWhenActivityStops(
        beforeStop = {
            composeRule.activityRule.scenario.moveToState(Lifecycle.State.STARTED)
            assertTrue("Pause without stop must preserve active media playback", isPlaying(it))
        },
    )

    @Test
    fun activeMediaPausesWhenActivityMovesDirectlyToStoppedState() = verifyPlayRequestWhenActivityStops()

    @Test
    fun activeMediaPausesWhenAndroidTvHomeKeyMinimizesTheActivity() {
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        assertTrue(
            "This test must run on an Android TV profile",
            context.packageManager.hasSystemFeature(PackageManager.FEATURE_LEANBACK),
        )

        var mediaFile: java.io.File? = null
        val player = AtomicReference<ExoPlayer>()

        try {
            mediaFile = PlayerTestMedia.createSilentWav(context)
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                player.set(
                    ExoPlayer.Builder(context).build().apply {
                        volume = 0f
                        setMediaItem(MediaItem.fromUri(Uri.fromFile(checkNotNull(mediaFile))))
                        prepare()
                        play()
                    },
                )
            }

            composeRule.setContent { PausePlaybackWhenActivityStops { player.get()?.pause() } }
            composeRule.waitForIdle()
            val activePlayer = checkNotNull(player.get())
            composeRule.waitUntil(timeoutMillis = 10_000) { isPlaying(activePlayer) }

            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand("input keyevent KEYCODE_HOME")
                .close()

            composeRule.waitUntil(timeoutMillis = 5_000) {
                composeRule.activityRule.scenario.state == Lifecycle.State.CREATED
            }
            composeRule.waitUntil(timeoutMillis = 5_000) { !isPlaying(activePlayer) }

            assertFalse("Pressing Home on Android TV must stop the Activity and pause media", isPlaying(activePlayer))
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync { player.getAndSet(null)?.release() }
            mediaFile?.delete()
        }
    }

    private fun verifyPlayRequestWhenActivityStops(beforeStop: (ExoPlayer) -> Unit = {}) {
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        val mediaFile = PlayerTestMedia.createSilentWav(context)
        val player = AtomicReference<ExoPlayer>()
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            player.set(
                ExoPlayer.Builder(context).build().apply {
                    volume = 0f
                    setMediaItem(MediaItem.fromUri(Uri.fromFile(mediaFile)))
                    prepare()
                    play()
                },
            )
        }

        try {
            composeRule.setContent { PausePlaybackWhenActivityStops { player.get().pause() } }
            composeRule.waitForIdle()
            val activePlayer = checkNotNull(player.get())
            composeRule.waitUntil(timeoutMillis = 10_000) { isPlaying(activePlayer) }
            beforeStop(activePlayer)
            composeRule.activityRule.scenario.moveToState(Lifecycle.State.CREATED)
            composeRule.waitUntil(timeoutMillis = 5_000) { !isPlaying(activePlayer) }

            assertFalse("Stopping the host Activity must pause active media playback", isPlaying(activePlayer))
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync { player.getAndSet(null)?.release() }
            mediaFile.delete()
        }
    }

    private fun isPlaying(player: ExoPlayer): Boolean {
        val result = AtomicBoolean()
        InstrumentationRegistry.getInstrumentation().runOnMainSync { result.set(player.isPlaying) }
        return result.get()
    }
}
