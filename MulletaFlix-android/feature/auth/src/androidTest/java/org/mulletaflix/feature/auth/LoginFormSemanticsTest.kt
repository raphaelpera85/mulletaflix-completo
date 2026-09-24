package org.mulletaflix.feature.auth

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class LoginFormSemanticsTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun stableTagsAcceptUsernameAndSpecialCharacterPassword() {
        var username by mutableStateOf("")
        var password by mutableStateOf("")
        var submitted = false

        composeRule.setContent {
            MaterialTheme {
                PasswordLoginForm(
                    username = username,
                    password = password,
                    isLoading = false,
                    error = null,
                    onUsernameChange = { username = it },
                    onPasswordChange = { password = it },
                    onLogin = { submitted = true },
                    users = emptyList(),
                    onUserSelect = {},
                    onRegister = {},
                )
            }
        }

        composeRule.onNodeWithTag(LOGIN_USERNAME_TEST_TAG).performTextInput("Raphael")
        composeRule.onNodeWithTag(LOGIN_PASSWORD_TEST_TAG).performTextInput("Bug309c*")
        composeRule.onNodeWithTag(LOGIN_SUBMIT_TEST_TAG).assertIsEnabled().performClick()

        composeRule.runOnIdle {
            assertTrue(username == "Raphael")
            assertTrue(password == "Bug309c*")
            assertTrue(submitted)
        }
    }

    @Test
    fun quickConnectInitiateButtonExposesStableTag() {
        var initiated = false

        composeRule.setContent {
            MaterialTheme {
                QuickConnectForm(
                    pin = null,
                    isAvailable = true,
                    isLoading = false,
                    isWaiting = false,
                    secondsRemaining = null,
                    error = null,
                    onInitiate = { initiated = true },
                    onCancel = {},
                    onCopyPin = {},
                )
            }
        }

        composeRule.onNodeWithTag(QUICK_CONNECT_INITIATE_TEST_TAG).performClick()

        composeRule.runOnIdle { assertTrue(initiated) }
    }
}
