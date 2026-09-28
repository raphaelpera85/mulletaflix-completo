package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.DeviceConfigurationOverride
import androidx.compose.ui.test.WindowSize
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipeUp
import androidx.compose.ui.unit.DpSize
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlin.math.abs
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class AdaptiveItemDetailLayoutTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun phoneKeepsSingleColumn() {
        assertLayout(widthDp = 411, isTelevision = false, isSplit = false)
    }

    @Test
    fun portraitTabletKeepsSingleColumn() {
        assertLayout(widthDp = 600, isTelevision = false, isSplit = false)
    }

    @Test
    fun expandedTabletUsesSupportingPane() {
        assertLayout(widthDp = 840, isTelevision = false, isSplit = true)
    }

    @Test
    fun televisionKeepsSingleColumnAtWideWidth() {
        assertLayout(widthDp = 840, isTelevision = true, isSplit = false)
    }

    @Test
    fun expandedTabletDetailsRemainIndependentlyScrollable() {
        setLayout(widthDp = 840, isTelevision = false)

        val heroTopBeforeScroll = composeRule.onNodeWithText("Poster")
            .fetchSemanticsNode().boundsInRoot.top
        val detailsTopBeforeScroll = composeRule.onNodeWithText("Detalhes")
            .fetchSemanticsNode().boundsInRoot.top
        composeRule.onNodeWithText("Último episódio").assertIsNotDisplayed()
        composeRule.onNodeWithTag("item-detail-details-scroll-pane").performTouchInput { swipeUp() }
        composeRule.onNodeWithText("Último episódio").assertIsDisplayed()
        composeRule.onNodeWithText("Poster").assertIsDisplayed()

        val heroTopAfterScroll = composeRule.onNodeWithText("Poster")
            .fetchSemanticsNode().boundsInRoot.top
        val detailsTopAfterScroll = composeRule.onNodeWithText("Detalhes")
            .fetchSemanticsNode().boundsInRoot.top
        assert(abs(heroTopAfterScroll - heroTopBeforeScroll) < 1f)
        assert(detailsTopAfterScroll < detailsTopBeforeScroll)
    }

    private fun assertLayout(widthDp: Int, isTelevision: Boolean, isSplit: Boolean) {
        setLayout(widthDp, isTelevision)

        val hero = composeRule.onNodeWithText("Poster").fetchSemanticsNode().boundsInRoot
        val details = composeRule.onNodeWithText("Detalhes").fetchSemanticsNode().boundsInRoot
        if (isSplit) {
            assert(details.left > hero.left)
            assert(abs(details.top - hero.top) < 1f)
        } else {
            assert(abs(details.left - hero.left) < 1f)
            assert(details.top > hero.top)
        }
    }

    private fun setLayout(widthDp: Int, isTelevision: Boolean) {
        composeRule.setContent {
            DeviceConfigurationOverride(
                DeviceConfigurationOverride.WindowSize(DpSize(widthDp.dp, 640.dp)),
            ) {
                MaterialTheme {
                    Box(Modifier.fillMaxSize()) {
                        AdaptiveItemDetailLayout(
                            isTelevision = isTelevision,
                            modifier = Modifier.fillMaxSize(),
                            hero = {
                                Box(Modifier.fillMaxWidth().height(120.dp)) {
                                    Text("Poster")
                                }
                            },
                            details = {
                                Column {
                                    Text("Detalhes")
                                    Spacer(Modifier.height(1_400.dp))
                                    Text("Último episódio")
                                }
                            },
                        )
                    }
                }
            }
        }
        composeRule.waitForIdle()
    }
}
