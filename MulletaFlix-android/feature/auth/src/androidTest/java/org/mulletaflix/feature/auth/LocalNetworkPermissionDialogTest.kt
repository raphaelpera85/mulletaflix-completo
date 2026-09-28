package org.mulletaflix.feature.auth

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class LocalNetworkPermissionDialogTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun continueInternetInvokesFallbackAction() {
        var continued = false

        composeRule.setContent {
            MaterialTheme {
                LocalNetworkPermissionDeniedDialog(
                    onDismiss = {},
                    onOpenSettings = {},
                    onContinueInternet = { continued = true },
                )
            }
        }

        composeRule.onNodeWithText("Acesso à rede local não permitido").assertIsDisplayed()
        composeRule.onNodeWithText("Continuar pela Internet").performClick()

        composeRule.runOnIdle { assertTrue(continued) }
    }
}
