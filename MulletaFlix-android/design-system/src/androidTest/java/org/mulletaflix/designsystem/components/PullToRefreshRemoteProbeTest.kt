package org.mulletaflix.designsystem.components

import android.os.Build
import androidx.compose.foundation.background
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PixelMap
import androidx.compose.ui.graphics.toPixelMap
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.filters.SdkSuppress
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * Probes whether a D-pad can leave the Material 3 pull-to-refresh indicator
 * revealed on screen.
 *
 * The report: "um símbolo de atualizar o tempo todo no meio da tela", clarified
 * as a circular arrow that does **not** spin and sits there "tipo um botão".
 * That is not a progress spinner — those rotate. It matches
 * `PullToRefreshDefaults.Indicator` while `isRefreshing == false` but the pull
 * distance is non-zero: it draws a static circular arrow in a raised circle at
 * the top-centre of the content and never animates.
 *
 * This test exists to confirm or refute that mechanism by measurement before any
 * production change is made. A non-zero difference means the D-pad really can
 * park the indicator; an identical capture means the hypothesis is wrong and no
 * fix should be written for it.
 */
@RunWith(AndroidJUnit4::class)
@SdkSuppress(minSdkVersion = Build.VERSION_CODES.O)
class PullToRefreshRemoteProbeTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun capture(): PixelMap = composeRule.onRoot().captureToImage().toPixelMap()

    private fun differingPixels(before: PixelMap, after: PixelMap): Int {
        if (before.width != after.width || before.height != after.height) return Int.MAX_VALUE
        var differing = 0
        for (y in 0 until before.height) {
            for (x in 0 until before.width) {
                if (before[x, y] != after[x, y]) differing++
            }
        }
        return differing
    }

    @Composable
    private fun PullableList(isRefreshing: Boolean) {
        MulletaFlixTheme {
            PullToRefreshBox(
                isRefreshing = isRefreshing,
                onRefresh = {},
                modifier = Modifier.fillMaxSize().background(Color.Black),
            ) {
                LazyColumn(Modifier.fillMaxSize()) {
                    items((0 until 30).toList()) { index ->
                        // The row itself must be the focusable node, so the
                        // remote's focus and the pull gesture act on the same
                        // element the user sees.
                        Text(
                            text = "Item $index",
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(80.dp)
                                .focusable(),
                        )
                    }
                }
            }
        }
    }

    @Test
    fun dpadPressAtTheTopOfTheListDoesNotRevealTheIndicator() {
        composeRule.setContent { PullableList(isRefreshing = false) }

        composeRule.onNodeWithText("Item 0").requestFocus()
        composeRule.waitForIdle()
        val before = capture()

        // What a remote does when the focus is already at the top of a TV list.
        composeRule.onNodeWithText("Item 0").performKeyInput { pressKey(Key.DirectionUp) }
        composeRule.waitForIdle()
        val after = capture()

        // Measured: 0 pixels change. `PullToRefreshBox` does not treat a D-pad
        // press as a pull, so it cannot park its indicator on a TV. Recorded
        // here because the opposite was the obvious explanation for a stationary
        // circular arrow on screen, and it is wrong — a future change that makes
        // the container react to key input would show up as a failure.
        assertEquals(
            "a remote press at the top of the list must leave the screen untouched",
            0,
            differingPixels(before, after),
        )
    }

    @Test
    fun anActiveRefreshDoesRevealTheIndicator() {
        // Control: proves the container really does draw the indicator in this
        // environment, so the probe above cannot pass vacuously.
        var refreshing by mutableStateOf(true)
        composeRule.setContent { PullableList(isRefreshing = refreshing) }

        composeRule.onNodeWithText("Item 0").requestFocus()
        composeRule.waitForIdle()
        val active = capture()

        composeRule.runOnIdle { refreshing = false }
        composeRule.waitForIdle()
        val idle = capture()

        assertTrue(
            "the enabled refresh state must be visibly different from the idle one",
            differingPixels(active, idle) > 0,
        )
    }
}
