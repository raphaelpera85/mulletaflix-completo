package org.mulletaflix.android

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onAllNodesWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class AppSmokeTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun serverSelectionShowsBrandingAndConnectionActions() {
        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.onAllNodesWithText("Procurar na rede", useUnmergedTree = true)
                .fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("MulletaFlix", useUnmergedTree = true).assertIsDisplayed()
        composeRule.onNodeWithText("URL do Servidor", useUnmergedTree = true).assertIsDisplayed()
        composeRule.onNodeWithText("Procurar na rede", useUnmergedTree = true).assertIsDisplayed()
    }

}
