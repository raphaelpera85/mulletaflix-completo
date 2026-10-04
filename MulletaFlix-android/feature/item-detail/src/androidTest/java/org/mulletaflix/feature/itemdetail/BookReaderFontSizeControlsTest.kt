package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class BookReaderFontSizeControlsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun controlsExposeScaleAndRespondToTouch() {
        var decreases = 0
        var increases = 0
        showControls(
            percent = 100,
            onDecrease = { decreases++ },
            onIncrease = { increases++ },
        )

        composeRule.onNodeWithText("100%").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Diminuir tamanho do texto")
            .assertIsEnabled()
            .performClick()
        composeRule.onNodeWithContentDescription("Aumentar tamanho do texto")
            .assertIsEnabled()
            .performClick()

        composeRule.runOnIdle {
            assertEquals(1, decreases)
            assertEquals(1, increases)
        }
    }

    @Test
    fun controlsDisableActionsAtSupportedBounds() {
        showControls(percent = BookReaderFontSize.MIN_PERCENT)
        composeRule.onNodeWithContentDescription("Diminuir tamanho do texto").assertIsNotEnabled()
        composeRule.onNodeWithContentDescription("Aumentar tamanho do texto").assertIsEnabled()
    }

    @Test
    fun increaseIsDisabledAtMaximum() {
        showControls(percent = BookReaderFontSize.MAX_PERCENT)
        composeRule.onNodeWithContentDescription("Diminuir tamanho do texto").assertIsEnabled()
        composeRule.onNodeWithContentDescription("Aumentar tamanho do texto").assertIsNotEnabled()
    }

    private fun showControls(
        percent: Int,
        onDecrease: () -> Unit = {},
        onIncrease: () -> Unit = {},
    ) {
        composeRule.setContent {
            MaterialTheme {
                BookReaderFontSizeControls(
                    fontSizePercent = percent,
                    enabled = true,
                    onDecrease = onDecrease,
                    onIncrease = onIncrease,
                )
            }
        }
    }
}
