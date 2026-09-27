package org.mulletaflix.feature.player

import android.app.PictureInPictureParams
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.util.Rational
import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.media3.common.MediaItem
import androidx.media3.exoplayer.ExoPlayer
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.filters.SdkSuppress
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

class PipPlaybackTestActivity : ComponentActivity()

@RunWith(AndroidJUnit4::class)
@SdkSuppress(minSdkVersion = Build.VERSION_CODES.O)
class PlayerPictureInPictureLifecycleTest {

    @get:Rule
    val composeRule = createAndroidComposeRule<PipPlaybackTestActivity>()

    @Test
    fun mediaKeepsPlayingInPipAndPausesWhenPipActivityCloses() {
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        assumeTrue(
            "This device does not advertise PiP support",
            context.packageManager.hasSystemFeature(PackageManager.FEATURE_PICTURE_IN_PICTURE),
        )

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
            composeRule.setContent {
                PausePlaybackWhenActivityStops { player.get().pause() }
            }
            composeRule.waitForIdle()
            composeRule.waitUntil(timeoutMillis = 10_000) { isPlaying(player.get()) }

            composeRule.activityRule.scenario.onActivity { activity ->
                activity.enterPictureInPictureMode(
                    PictureInPictureParams.Builder()
                        .setAspectRatio(Rational(16, 9))
                        .build(),
                )
            }
            val isInPipMode = AtomicBoolean()
            composeRule.waitUntil(timeoutMillis = 5_000) {
                composeRule.activityRule.scenario.onActivity { isInPipMode.set(it.isInPictureInPictureMode) }
                isInPipMode.get()
            }

            assertTrue("Media must keep playing while the system PiP window is visible", isPlaying(player.get()))

            composeRule.activityRule.scenario.onActivity { activity ->
                assertTrue("The test must close an actual system PiP window", activity.isInPictureInPictureMode)
                activity.finish()
            }
            composeRule.waitUntil(timeoutMillis = 5_000) { !isPlaying(player.get()) }

            assertFalse("Closing the PiP activity must pause media", isPlaying(player.get()))
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                player.getAndSet(null)?.release()
            }
            mediaFile.delete()
        }
    }

    private fun isPlaying(player: ExoPlayer): Boolean {
        val result = AtomicBoolean()
        InstrumentationRegistry.getInstrumentation().runOnMainSync { result.set(player.isPlaying) }
        return result.get()
    }
}
