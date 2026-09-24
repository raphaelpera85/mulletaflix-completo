package org.mulletaflix.feature.home

import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import org.junit.Rule
import org.junit.Test
import org.junit.Assert.assertEquals
import java.util.concurrent.atomic.AtomicInteger

/** Regression coverage for TV refresh after leaving and re-entering the foreground. */
class TvRefreshEffectTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun refreshes_immediately_each_time_tv_returns_to_resumed() {
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

        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
        }
        composeRule.waitUntil { refreshCount.get() == 1 }

        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
        }
        composeRule.waitUntil { refreshCount.get() == 2 }

        assertEquals(2, refreshCount.get())
    }

    private class TestLifecycleOwner : LifecycleOwner {
        val registry = LifecycleRegistry(this)
        override val lifecycle: Lifecycle = registry
    }
}
