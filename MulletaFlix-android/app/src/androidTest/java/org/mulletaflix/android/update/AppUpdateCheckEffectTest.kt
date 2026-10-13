package org.mulletaflix.android.update

import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.runtime.mutableStateOf
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.atomic.AtomicInteger

class AppUpdateCheckEffectTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun disabling_a_running_effect_stops_future_update_callbacks() {
        val owner = TestLifecycleOwner()
        val enabled = mutableStateOf(true)
        val checks = AtomicInteger()
        composeRule.runOnUiThread { owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE) }

        composeRule.setContent {
            AppUpdateCheckEffect(owner, intervalMillis = 100L, enabled = enabled.value) {
                checks.incrementAndGet()
            }
        }
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
        }
        composeRule.waitUntil(timeoutMillis = 2_000) { checks.get() >= 2 }
        val checksBeforeDisable = checks.get()
        composeRule.runOnUiThread { enabled.value = false }
        composeRule.mainClock.advanceTimeBy(350)

        assertEquals(checksBeforeDisable, checks.get())
    }

    @Test
    fun periodic_check_runs_while_resumed_and_stops_in_background() {
        val owner = TestLifecycleOwner()
        val checks = AtomicInteger()
        composeRule.runOnUiThread { owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_CREATE) }

        composeRule.setContent {
            AppUpdateCheckEffect(owner, intervalMillis = 100L) { checks.incrementAndGet() }
        }
        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_START)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_RESUME)
        }
        composeRule.waitUntil(timeoutMillis = 2_000) { checks.get() >= 2 }

        composeRule.runOnUiThread {
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_PAUSE)
            owner.registry.handleLifecycleEvent(Lifecycle.Event.ON_STOP)
        }
        val stoppedAt = checks.get()
        Thread.sleep(250)

        assertEquals(stoppedAt, checks.get())
    }

    private class TestLifecycleOwner : LifecycleOwner {
        val registry = LifecycleRegistry(this)
        override val lifecycle: Lifecycle = registry
    }
}
