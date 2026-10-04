package org.mulletaflix.feature.itemdetail

import android.content.res.Configuration
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.input.key.Key
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class BookReaderProgressActionsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun restartRequiresConfirmationAndCanBeCancelled() {
        var restartRequests = 0
        showActions { restartRequests++ }

        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Reiniciar do começo").performClick()
        composeRule.onNodeWithText("Reiniciar leitura?").assertIsDisplayed()
        composeRule.onNodeWithText("Cancelar").performClick()

        assertEquals(0, restartRequests)
    }

    @Test
    fun tvRemoteCanConfirmRestartFromTheMenu() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val isTelevision = (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        assumeTrue("D-pad restart behavior is specific to Android TV", isTelevision)

        var restartRequests = 0
        showActions { restartRequests++ }

        val menuButton = composeRule.onNodeWithContentDescription("Mais opções de leitura")
        menuButton.requestFocus().assertIsFocused()
        menuButton.performKeyInput { pressKey(Key.DirectionCenter) }

        val restartOption = composeRule.onNodeWithText("Reiniciar do começo")
        restartOption.assertIsDisplayed().requestFocus().assertIsFocused()
        restartOption.performKeyInput { pressKey(Key.DirectionCenter) }

        composeRule.onNodeWithText("Reiniciar leitura?").assertIsDisplayed()
        val confirmButton = composeRule.onNodeWithText("Reiniciar")
        confirmButton.requestFocus().assertIsFocused()
        confirmButton.performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.runOnIdle { assertEquals(1, restartRequests) }
    }

    private fun showActions(onRestart: () -> Unit) {
        composeRule.setContent {
            MaterialTheme {
                BookReaderProgressActions(enabled = true, onRestart = onRestart)
            }
        }
    }
}
