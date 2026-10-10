package org.mulletaflix.feature.library

import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicBoolean

/** Regression coverage for library refresh after returning to the TV foreground. */
class TvRefreshEffectTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun refreshes_immediately_each_time_library_returns_to_resumed() {
        val owner = TestLifecycleOwner()
        val refreshCount = AtomicInteger(0)
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
        }

        composeRule.setContent {
            TvRefreshEffect(
                lifecycleOwner = owner,
                refreshIntervalMillis = Long.MAX_VALUE,
                refreshImmediately = true,
                onRefresh = { refreshCount.incrementAndGet() },
            )
        }

        resume(owner)
        composeRule.waitUntil { refreshCount.get() == 1 }

        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
        }
        resume(owner)
        composeRule.waitUntil { refreshCount.get() == 2 }

        assertEquals(2, refreshCount.get())
    }

    @Test
    fun uses_resume_callback_separately_from_periodic_callback() {
        val owner = TestLifecycleOwner()
        val periodicCount = AtomicInteger(0)
        val resumeCount = AtomicInteger(0)
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
        }

        composeRule.setContent {
            TvRefreshEffect(
                lifecycleOwner = owner,
                refreshIntervalMillis = Long.MAX_VALUE,
                refreshImmediately = true,
                onRefresh = { periodicCount.incrementAndGet() },
                onResumeRefresh = { resumeCount.incrementAndGet() },
            )
        }

        resume(owner)
        composeRule.waitUntil { resumeCount.get() == 1 }
        assertEquals(0, periodicCount.get())

        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
        }
        resume(owner)
        composeRule.waitUntil { resumeCount.get() == 2 }

        assertEquals(0, periodicCount.get())
    }

    @Test
    fun keeps_refreshing_after_one_refresh_failure() {
        val owner = TestLifecycleOwner()
        val refreshCount = AtomicInteger(0)
        val failOnce = AtomicBoolean(true)
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE)
        }

        composeRule.setContent {
            TvRefreshEffect(
                lifecycleOwner = owner,
                refreshIntervalMillis = 50,
                refreshImmediately = true,
                onRefresh = {
                    refreshCount.incrementAndGet()
                    if (failOnce.compareAndSet(true, false)) error("transient refresh failure")
                },
            )
        }

        resume(owner)
        composeRule.waitUntil(timeoutMillis = 2_000) { refreshCount.get() >= 2 }

        assertEquals(false, failOnce.get())
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
