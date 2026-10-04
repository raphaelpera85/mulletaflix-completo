package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
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

    @Test
    fun actionsAreDisabledBeforeTheRenditionIsReady() {
        showControls(percent = 100, enabled = false)
        composeRule.onNodeWithContentDescription("Diminuir tamanho do texto").assertIsNotEnabled()
        composeRule.onNodeWithContentDescription("Aumentar tamanho do texto").assertIsNotEnabled()
    }

    @Test
    fun navigationAndFontControlsFitCompactPhoneWidth() {
        composeRule.setContent {
            MaterialTheme {
                Box(Modifier.width(320.dp).testTag("compact-reader-controls")) {
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(horizontal = 8.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        IconButton(
                            onClick = {},
                            modifier = Modifier.semantics(mergeDescendants = true) {
                                contentDescription = "Página anterior"
                            },
                        ) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = null)
                        }
                        BookReaderFontSizeControls(
                            fontSizePercent = 100,
                            enabled = true,
                            onDecrease = {},
                            onIncrease = {},
                        )
                        IconButton(
                            onClick = {},
                            modifier = Modifier.semantics(mergeDescendants = true) {
                                contentDescription = "Próxima página"
                            },
                        ) {
                            Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = null)
                        }
                    }
                }
            }
        }

        val viewport = composeRule.onNodeWithTag("compact-reader-controls").fetchSemanticsNode().boundsInRoot
        listOf(
            "Página anterior",
            "Diminuir tamanho do texto",
            "Aumentar tamanho do texto",
            "Próxima página",
        ).forEach { contentDescription ->
            val bounds = composeRule.onNodeWithContentDescription(contentDescription).fetchSemanticsNode().boundsInRoot
            assertTrue("$contentDescription stays inside 320dp width", bounds.left >= viewport.left && bounds.right <= viewport.right)
            val minimumTouchTarget = with(composeRule.density) { 48.dp.toPx() }
            assertTrue("$contentDescription has at least a 48dp target", bounds.width >= minimumTouchTarget)
        }
    }

    private fun showControls(
        percent: Int,
        enabled: Boolean = true,
        onDecrease: () -> Unit = {},
        onIncrease: () -> Unit = {},
    ) {
        composeRule.setContent {
            MaterialTheme {
                BookReaderFontSizeControls(
                    fontSizePercent = percent,
                    enabled = enabled,
                    onDecrease = onDecrease,
                    onIncrease = onIncrease,
                )
            }
        }
    }
}
