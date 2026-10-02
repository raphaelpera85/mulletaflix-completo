package org.mulletaflix.feature.auth

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class LocalNetworkDiscoveryRetryTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun manualSearchReopensDismissedPermissionRationale() {
        val permissionPromptDismissed = mutableStateOf(false)
        var discoveryRequests = 0
        var permissionRequests = 0

        composeRule.setContent {
            MaterialTheme {
                LocalNetworkDiscoveryControl(
                    isDiscovering = false,
                    isLoading = false,
                    permissionRequired = true,
                    permissionRequestDenied = false,
                    permissionPromptDismissed = permissionPromptDismissed.value,
                    onPermissionPromptDismissedChange = { permissionPromptDismissed.value = it },
                    onDiscover = { discoveryRequests++ },
                    onRequestPermission = { permissionRequests++ },
                )
            }
        }

        composeRule.onNodeWithText("Encontrar servidor na rede local").assertIsDisplayed()
        composeRule.onNodeWithText("Agora não").performClick()
        composeRule.onAllNodesWithText("Encontrar servidor na rede local").assertCountEquals(0)

        composeRule.onNodeWithText("Procurar na rede").performClick()
        composeRule.onNodeWithText("Encontrar servidor na rede local").assertIsDisplayed()
        composeRule.onNodeWithText("Permitir").performClick()

        composeRule.runOnIdle {
            assertEquals(1, discoveryRequests)
            assertEquals(1, permissionRequests)
        }
    }
}
