package org.mulletaflix.feature.player

import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.lifecycle.Lifecycle
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
    fun playRequestRemainsActiveOnPauseThenClearsWhenActivityStops() = verifyPlayRequestWhenActivityStops(
        beforeStop = {
            composeRule.activityRule.scenario.moveToState(Lifecycle.State.STARTED)
            assertTrue("Pause without stop must preserve the player's play request", isPlayWhenReady(it))
        },
    )

    @Test
    fun playRequestClearsWhenActivityMovesDirectlyToStoppedState() = verifyPlayRequestWhenActivityStops()

    private fun verifyPlayRequestWhenActivityStops(beforeStop: (ExoPlayer) -> Unit = {}) {
        val player = AtomicReference<ExoPlayer>()
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            player.set(ExoPlayer.Builder(ApplicationProvider.getApplicationContext()).build().apply { play() })
        }

        try {
            composeRule.setContent { PausePlaybackWhenActivityStops { player.get().pause() } }
            composeRule.waitForIdle()
            val activePlayer = checkNotNull(player.get())
            beforeStop(activePlayer)
            composeRule.activityRule.scenario.moveToState(Lifecycle.State.CREATED)

            assertFalse("Stopping the host Activity must clear the player's play request", isPlayWhenReady(activePlayer))
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync { player.getAndSet(null)?.release() }
        }
    }

    private fun isPlayWhenReady(player: ExoPlayer): Boolean {
        val result = AtomicBoolean()
        InstrumentationRegistry.getInstrumentation().runOnMainSync { result.set(player.playWhenReady) }
        return result.get()
    }
}
