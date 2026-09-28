package org.mulletaflix.feature.auth

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class LocalNetworkPermissionDialogTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun continueInternetInvokesFallbackAction() {
        var fallbackUrl: String? = null

        composeRule.setContent {
            MaterialTheme {
                LocalNetworkPermissionDeniedDialog(
                    onDismiss = {},
                    onOpenSettings = {},
                    onContinueInternet = { fallbackUrl = it },
                )
            }
        }

        composeRule.onNodeWithText("Acesso à rede local não permitido").assertIsDisplayed()
        composeRule.onNodeWithText("Continuar pela Internet").performClick()

        composeRule.runOnIdle {
            assertEquals(DEFAULT_MULLETAFLIX_SERVER_URL, fallbackUrl)
            assertTrue(fallbackUrl?.startsWith("http://") == true)
        }
    }
}
