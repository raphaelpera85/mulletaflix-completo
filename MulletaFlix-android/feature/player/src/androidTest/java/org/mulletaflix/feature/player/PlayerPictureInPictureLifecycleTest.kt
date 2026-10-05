package org.mulletaflix.feature.player

import android.app.PictureInPictureParams
import android.accessibilityservice.AccessibilityServiceInfo
import android.content.pm.PackageManager
import android.graphics.Rect
import android.net.Uri
import android.os.Build
import android.os.SystemClock
import android.util.Rational
import android.view.MotionEvent
import android.view.accessibility.AccessibilityNodeInfo
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

class PipPlaybackTestActivity : ComponentActivity() {
    override fun onDestroy() {
        destroyed.set(true)
        super.onDestroy()
    }

    companion object {
        val destroyed = AtomicBoolean()
    }
}

@RunWith(AndroidJUnit4::class)
@SdkSuppress(minSdkVersion = Build.VERSION_CODES.O)
class PlayerPictureInPictureLifecycleTest {

    @get:Rule
    val composeRule = createAndroidComposeRule<PipPlaybackTestActivity>()

    @Test
    fun mediaKeepsPlayingInPipAndPausesWhenPipActivityCloses() {
        PipPlaybackTestActivity.destroyed.set(false)
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

        val automation = InstrumentationRegistry.getInstrumentation().uiAutomation
        val automationServiceInfo = automation.serviceInfo
        val originalAccessibilityFlags = automationServiceInfo.flags
        var accessibilityFlagsTouched = false

        try {
            automationServiceInfo.flags = originalAccessibilityFlags or
                AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS
            accessibilityFlagsTouched = true
            automation.setServiceInfo(automationServiceInfo)

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

            val pipBounds = AtomicReference<Rect>()
            val targetPackage = AtomicReference<String>()
            val screenSize = AtomicReference<Pair<Int, Int>>()
            composeRule.activityRule.scenario.onActivity { activity ->
                assertTrue("The test must close an actual system PiP window", activity.isInPictureInPictureMode)
                targetPackage.set(activity.packageName)
                screenSize.set(activity.resources.displayMetrics.widthPixels to activity.resources.displayMetrics.heightPixels)
            }
            val pipVisible = runCatching {
                composeRule.waitUntil(timeoutMillis = 5_000) {
                    pipBounds.set(
                        findPictureInPictureBounds(
                            automation,
                            targetPackage.get(),
                            screenSize.get().first,
                            screenSize.get().second,
                        ),
                    )
                    pipBounds.get() != null
                }
            }.isSuccess
            assertTrue(
                "Could not find the app's bounded PiP window. Accessibility windows: ${accessibilitySnapshot(automation)}",
                pipVisible,
            )
            val bounds = requireNotNull(pipBounds.get())
            tap(automation, bounds.centerX(), bounds.centerY())

            val closeAction = AtomicReference<AccessibilityNodeInfo?>()
            val closeControlVisible = runCatching {
                composeRule.waitUntil(timeoutMillis = 5_000) {
                    closeAction.set(findSystemCloseAction(automation, bounds))
                    closeAction.get() != null
                }
            }.isSuccess
            assertTrue(
                "System PiP close control not found at $bounds. Accessibility tree: ${accessibilitySnapshot(automation)}",
                closeControlVisible,
            )
            val closeNode = requireNotNull(closeAction.get()) {
                "System PiP close control not found. Accessibility tree: ${accessibilitySnapshot(automation)}"
            }
            val closeActionAccepted = try {
                closeNode.performAction(AccessibilityNodeInfo.ACTION_CLICK)
            } finally {
                closeNode.recycle()
            }
            assertTrue("System PiP close control must accept activation", closeActionAccepted)

            composeRule.waitUntil(timeoutMillis = 5_000) {
                PipPlaybackTestActivity.destroyed.get() &&
                    !isPlaying(player.get()) &&
                    findPictureInPictureBounds(
                        automation,
                        targetPackage.get(),
                        screenSize.get().first,
                        screenSize.get().second,
                    ) == null
            }

            assertTrue("Tapping the system PiP close control must close its activity", PipPlaybackTestActivity.destroyed.get())
            assertFalse("Closing the PiP activity must pause media", isPlaying(player.get()))
            assertTrue(
                "The app's system PiP window must disappear after close",
                findPictureInPictureBounds(
                    automation,
                    targetPackage.get(),
                    screenSize.get().first,
                    screenSize.get().second,
                ) == null,
            )
        } finally {
            try {
                if (accessibilityFlagsTouched) {
                    automationServiceInfo.flags = originalAccessibilityFlags
                    automation.setServiceInfo(automationServiceInfo)
                }
            } finally {
                try {
                    InstrumentationRegistry.getInstrumentation().runOnMainSync {
                        player.getAndSet(null)?.release()
                    }
                } finally {
                    mediaFile.delete()
                }
            }
        }
    }

    private fun isPlaying(player: ExoPlayer): Boolean {
        val result = AtomicBoolean()
        InstrumentationRegistry.getInstrumentation().runOnMainSync { result.set(player.isPlaying) }
        return result.get()
    }

    private fun tap(automation: android.app.UiAutomation, x: Int, y: Int) {
        val downTime = SystemClock.uptimeMillis()
        val down = MotionEvent.obtain(downTime, downTime, MotionEvent.ACTION_DOWN, x.toFloat(), y.toFloat(), 0)
        val up = MotionEvent.obtain(downTime, downTime + 80, MotionEvent.ACTION_UP, x.toFloat(), y.toFloat(), 0)
        try {
            assertTrue("Could not tap the system PiP window", automation.injectInputEvent(down, true))
            assertTrue("Could not release the system PiP tap", automation.injectInputEvent(up, true))
        } finally {
            down.recycle()
            up.recycle()
        }
    }

    private fun findSystemCloseAction(
        automation: android.app.UiAutomation,
        pipBounds: Rect,
    ): AccessibilityNodeInfo? {
        automation.rootInActiveWindow?.let { root ->
            findSystemCloseAction(root, pipBounds)?.let { return it }
        }
        for (window in automation.windows) {
            try {
                val root = window.root ?: continue
                findSystemCloseAction(root, pipBounds)?.let { return it }
            } finally {
                window.recycle()
            }
        }
        return null
    }

    private fun findPictureInPictureBounds(
        automation: android.app.UiAutomation,
        packageName: String,
        screenWidth: Int,
        screenHeight: Int,
    ): Rect? {
        for (window in automation.windows) {
            try {
                val root = window.root ?: continue
                try {
                    if (root.packageName?.toString() != packageName) continue
                    val bounds = Rect()
                    window.getBoundsInScreen(bounds)
                    val isSmallerThanFullscreen =
                        bounds.width().toLong() * 4 <= screenWidth.toLong() * 3 &&
                            bounds.height().toLong() * 4 <= screenHeight.toLong() * 3
                    if (bounds.width() > 0 && bounds.height() > 0 && isSmallerThanFullscreen) return bounds
                } finally {
                    root.recycle()
                }
            } finally {
                window.recycle()
            }
        }
        return null
    }

    private fun findSystemCloseAction(node: AccessibilityNodeInfo, pipBounds: Rect): AccessibilityNodeInfo? {
        var keepNode = false
        try {
            val label = sequenceOf(node.contentDescription, node.text, node.viewIdResourceName)
                .filterNotNull()
                .joinToString(" ")
                .lowercase()
            val isSystemUi = node.packageName?.toString() == "com.android.systemui"
            val describesClose = label.split(Regex("[^\\p{L}]+"))
                .any { it == "close" || it == "fechar" }
            val nodeBounds = Rect()
            node.getBoundsInScreen(nodeBounds)
            if (isSystemUi && node.isVisibleToUser && node.isClickable && describesClose &&
                Rect.intersects(nodeBounds, pipBounds)
            ) {
                keepNode = true
                return node
            }

            for (index in 0 until node.childCount) {
                val child = node.getChild(index) ?: continue
                findSystemCloseAction(child, pipBounds)?.let { return it }
            }
            return null
        } finally {
            if (!keepNode) node.recycle()
        }
    }

    private fun accessibilitySnapshot(automation: android.app.UiAutomation): String =
        automation.windows.map { window ->
            try {
                val bounds = Rect()
                window.getBoundsInScreen(bounds)
                val root = window.root
                val description = root?.let { rootNode ->
                    try {
                        describeNode(rootNode)
                    } finally {
                        rootNode.recycle()
                    }
                }
                "type=${window.type}, bounds=$bounds, root=$description"
            } finally {
                window.recycle()
            }
        }
            .joinToString(" | ")
            .take(6_000)

    private fun describeNode(node: AccessibilityNodeInfo): String {
        val label = sequenceOf(node.contentDescription, node.text, node.viewIdResourceName)
            .filterNotNull()
            .joinToString("/")
        val children = (0 until node.childCount).mapNotNull { index ->
            node.getChild(index)?.let { child ->
                try {
                    describeNode(child)
                } finally {
                    child.recycle()
                }
            }
        }
        return "${node.packageName}:${node.className}[$label]" + children.joinToString(prefix = "{", postfix = "}")
    }
}
