package org.mulletaflix.feature.player

import androidx.compose.runtime.CompositionLocalProvider
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.test.junit4.v2.createComposeRule
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class PlayerBackgroundPlaybackTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun playbackPausesWhenActivityStopsAfterPipCloses() {
        val owner = TestLifecycleOwner()
        var pauseCount = 0
        composeRule.setContent {
            CompositionLocalProvider(LocalLifecycleOwner provides owner) {
                PausePlaybackWhenActivityStops { pauseCount++ }
            }
        }

        composeRule.runOnIdle {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            assertEquals("PiP remains active while Activity is only paused", 0, pauseCount)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
            assertEquals("Closing PiP stops Activity and pauses playback", 1, pauseCount)
        }
    }

    private class TestLifecycleOwner : LifecycleOwner {
        val registry = LifecycleRegistry(this)
        override val lifecycle: Lifecycle get() = registry
    }
}
