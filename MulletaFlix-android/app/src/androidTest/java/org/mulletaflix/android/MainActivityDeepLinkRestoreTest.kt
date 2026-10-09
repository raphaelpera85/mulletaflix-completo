package org.mulletaflix.android

import android.content.Intent
import android.net.Uri
import androidx.compose.runtime.MutableState
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class MainActivityDeepLinkRestoreTest {
    @Test
    fun consumedLaunchLinkDoesNotReturnAfterActivityRecreation() {
        val scenario = launchWithMediaLink()

        try {
            scenario.onActivity { activity ->
                val pending = pendingDeepLinkState(activity)
                check(pending.value != null) { "The launch link was not accepted" }
                // Match the production NavHost callback after it reaches the destination.
                pending.value = null
            }

            scenario.recreate()

            scenario.onActivity { activity ->
                assertNull("A consumed launch link must stay consumed", pendingDeepLinkState(activity).value)
            }
        } finally {
            scenario.close()
        }
    }

    @Test
    fun pendingLaunchLinkKeepsItsSequenceAfterActivityRecreation() {
        val scenario = launchWithMediaLink()

        try {
            scenario.onActivity { activity ->
                deliverNewIntent(activity)
                assertEquals(2L, sequenceOf(pendingDeepLinkState(activity).value))
            }

            scenario.recreate()

            scenario.onActivity { activity ->
                assertEquals(2L, sequenceOf(pendingDeepLinkState(activity).value))
                deliverNewIntent(activity)
                assertEquals(3L, sequenceOf(pendingDeepLinkState(activity).value))
            }
        } finally {
            scenario.close()
        }
    }

    private fun launchWithMediaLink(): ActivityScenario<MainActivity> {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        return ActivityScenario.launch(mediaLinkIntent(context))
    }

    private fun mediaLinkIntent(
        context: android.content.Context = InstrumentationRegistry.getInstrumentation().targetContext,
    ): Intent = Intent(context, MainActivity::class.java).setData(
            Uri.parse("mulletaflix://details?id=deep-link-restore-test"),
        )

    private fun deliverNewIntent(activity: MainActivity) {
        val callback = MainActivity::class.java.getDeclaredMethod("onNewIntent", Intent::class.java)
        callback.isAccessible = true
        callback.invoke(activity, mediaLinkIntent())
    }

    private fun pendingDeepLinkState(activity: MainActivity): MutableState<Any?> {
        val delegate = activity.javaClass.declaredFields.single { field ->
            field.name.startsWith("incomingDeepLink") &&
                MutableState::class.java.isAssignableFrom(field.type)
        }
        delegate.isAccessible = true
        @Suppress("UNCHECKED_CAST")
        return delegate.get(activity) as MutableState<Any?>
    }

    private fun sequenceOf(request: Any?): Long {
        val getter = requireNotNull(request).javaClass.getDeclaredMethod("getSequence")
        return getter.invoke(request) as Long
    }
}
