package org.mulletaflix.feature.auth

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipeUp
import org.junit.Rule
import org.junit.Test

class RegisterDialogTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun passwordAndConfirmationHaveIndependentVisibilityControls() {
        composeRule.setContent {
            MaterialTheme {
                RegisterDialog(
                    isLoading = false,
                    error = null,
                    onDismiss = {},
                    onRegister = { _, _, _ -> },
                )
            }
        }

        composeRule.onAllNodesWithContentDescription("Mostrar senha").assertCountEquals(1)
        composeRule.onAllNodesWithContentDescription("Mostrar confirmação").assertCountEquals(1)

        composeRule.onAllNodesWithContentDescription("Mostrar senha")[0].performClick()

        composeRule.onAllNodesWithContentDescription("Ocultar senha").assertCountEquals(1)
        composeRule.onAllNodesWithContentDescription("Mostrar confirmação").assertCountEquals(1)
    }

    @Test
    fun longRegistrationErrorCanBeReachedByScrollingDialogContent() {
        val longError = "Mensagem inicial\n\n\n\n\n\nMensagem final"
        composeRule.setContent {
            MaterialTheme {
                RegisterDialog(
                    isLoading = false,
                    error = longError,
                    onDismiss = {},
                    onRegister = { _, _, _ -> },
                )
            }
        }

        composeRule
            .onNodeWithContentDescription(REGISTER_DIALOG_CONTENT_DESCRIPTION)
            .performTouchInput { swipeUp() }
        composeRule.onAllNodesWithText(longError, substring = true).assertCountEquals(1)
    }
}
