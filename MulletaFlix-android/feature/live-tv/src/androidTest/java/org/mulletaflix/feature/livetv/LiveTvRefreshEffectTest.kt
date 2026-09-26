package org.mulletaflix.feature.livetv

import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger

/** Regression coverage for channel and EPG refresh timers on Android TV. */
class LiveTvRefreshEffectTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun keeps_refreshing_after_one_transient_failure() {
        val owner = TestLifecycleOwner()
        val refreshCount = AtomicInteger(0)
        val failOnce = AtomicBoolean(true)
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
        }

        composeRule.setContent {
            LiveTvRefreshEffect(
                lifecycleOwner = owner,
                refreshIntervalMillis = 50,
                refreshImmediately = true,
                onRefresh = {
                    refreshCount.incrementAndGet()
                    if (failOnce.compareAndSet(true, false)) error("transient live tv failure")
                },
            )
        }
        resume(owner)

        composeRule.waitUntil(timeoutMillis = 2_000) { refreshCount.get() >= 2 }
        assertEquals(false, failOnce.get())
    }

    @Test
    fun stops_refreshing_when_the_screen_is_paused() {
        val owner = TestLifecycleOwner()
        val refreshCount = AtomicInteger(0)
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
        }

        composeRule.setContent {
            LiveTvRefreshEffect(
                lifecycleOwner = owner,
                refreshIntervalMillis = 50,
                refreshImmediately = true,
                onRefresh = { refreshCount.incrementAndGet() },
            )
        }
        resume(owner)
        composeRule.waitUntil(timeoutMillis = 2_000) { refreshCount.get() >= 2 }
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
        }
        val pausedCount = refreshCount.get()
        Thread.sleep(150)

        assertEquals(pausedCount, refreshCount.get())
    }

    @Test
    fun refreshes_immediately_when_returning_to_resumed_with_the_guide_open() {
        val owner = TestLifecycleOwner()
        val refreshCount = AtomicInteger(0)
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
        }

        composeRule.setContent {
            LiveTvRefreshEffect(
                lifecycleOwner = owner,
                refreshIntervalMillis = Long.MAX_VALUE,
                refreshImmediately = refreshLiveTvGuideImmediatelyOnResume(
                    isGuideOpen = true,
                    isTelevision = false,
                ),
                onRefresh = { refreshCount.incrementAndGet() },
            )
        }
        resume(owner)
        composeRule.waitUntil(timeoutMillis = 2_000) { refreshCount.get() == 1 }

        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
        }
        resume(owner)
        composeRule.waitUntil(timeoutMillis = 2_000) { refreshCount.get() == 2 }

        assertEquals(2, refreshCount.get())
    }

    private fun resume(owner: TestLifecycleOwner) {
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
        }
    }

    private class TestLifecycleOwner : LifecycleOwner {
        val registry = LifecycleRegistry(this)
        override val lifecycle: Lifecycle = registry
    }
}
