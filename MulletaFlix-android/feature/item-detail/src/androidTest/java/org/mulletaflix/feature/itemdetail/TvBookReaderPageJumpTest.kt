package org.mulletaflix.feature.itemdetail

import android.content.res.Configuration
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.performTextClearance
import androidx.compose.ui.test.performTextInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class TvBookReaderPageJumpTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun dpadCanSelectAndOpenARequestedPage() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val uiMode = context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK
        assertEquals(Configuration.UI_MODE_TYPE_TELEVISION, uiMode)

        val currentPage = mutableIntStateOf(0)
        val selectedPage = mutableIntStateOf(-1)
        composeRule.setContent {
            MaterialTheme {
                ComicBookPageControls(
                    currentPage = currentPage.intValue,
                    pageCount = 4,
                    onPageSelected = {
                        selectedPage.intValue = it
                        currentPage.intValue = it
                    },
                )
            }
        }

        val jumpButton = composeRule.onNodeWithContentDescription("Ir para página", substring = true)
        jumpButton.requestFocus().assertIsFocused()
        jumpButton.performKeyInput { pressKey(Key.DirectionCenter) }

        val pageNumber = composeRule.onNodeWithTag("page-number-input")
        pageNumber.assertIsDisplayed().requestFocus().assertIsFocused()
        pageNumber.performTextClearance()
        pageNumber.performTextInput("3")

        val confirmButton = composeRule.onNodeWithText("Ir")
        confirmButton.requestFocus().assertIsFocused()
        confirmButton.performKeyInput { pressKey(Key.DirectionCenter) }

        composeRule.onNodeWithText("Página 3 de 4").assertIsDisplayed()
        composeRule.runOnIdle {
            assertTrue("Selected page must be zero-based", selectedPage.intValue == 2)
        }
    }
}
