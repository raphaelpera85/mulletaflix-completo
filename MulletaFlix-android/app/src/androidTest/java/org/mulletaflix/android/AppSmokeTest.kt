package org.mulletaflix.android

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
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
            composeRule.onAllNodesWithText("MulletaFlix", useUnmergedTree = true)
                .fetchSemanticsNodes().isNotEmpty() &&
                (composeRule.onAllNodesWithText("Procurar na rede", useUnmergedTree = true)
                    .fetchSemanticsNodes().isNotEmpty() ||
                    composeRule.onAllNodesWithText("Usuário", useUnmergedTree = true)
                        .fetchSemanticsNodes().isNotEmpty())
        }
        composeRule.onNodeWithText("MulletaFlix", useUnmergedTree = true).assertIsDisplayed()
        val serverSelectionVisible = composeRule.onAllNodesWithText(
            "URL do Servidor",
            useUnmergedTree = true,
        ).fetchSemanticsNodes().isNotEmpty()
        if (serverSelectionVisible) {
            composeRule.onNodeWithText("URL do Servidor", useUnmergedTree = true).assertIsDisplayed()
            composeRule.onNodeWithText("Procurar na rede", useUnmergedTree = true).assertIsDisplayed()
        } else {
            // A live server may be verified and advance automatically to login.
            // In that case the smoke test validates the next deterministic step.
            composeRule.onNodeWithText("Usuário", useUnmergedTree = true).assertIsDisplayed()
            check(
                composeRule.onAllNodesWithText("Entrar", useUnmergedTree = true)
                    .fetchSemanticsNodes().isNotEmpty()
            ) { "O fluxo de login não exibiu a ação Entrar" }
        }
    }

}
