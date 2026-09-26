package org.mulletaflix.feature.auth

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
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
                    availabilityError = null,
                    isLoading = false,
                    isWaiting = false,
                    secondsRemaining = null,
                    error = null,
                    onInitiate = { initiated = true },
                    onRetryAvailability = {},
                    onCancel = {},
                    onCopyPin = {},
                )
            }
        }

        composeRule.onNodeWithTag(QUICK_CONNECT_INITIATE_TEST_TAG).performClick()

        composeRule.runOnIdle { assertTrue(initiated) }
    }

    @Test
    fun quickConnectPollTimeoutShowsNeutralMessageAndAllowsNewCode() {
        var initiated = false
        composeRule.setContent {
            MaterialTheme {
                QuickConnectForm(
                    pin = null,
                    isAvailable = true,
                    availabilityError = null,
                    isLoading = false,
                    isWaiting = false,
                    secondsRemaining = null,
                    error = QUICK_CONNECT_POLL_TIMEOUT_MESSAGE,
                    onInitiate = { initiated = true },
                    onRetryAvailability = {},
                    onCancel = {},
                    onCopyPin = {},
                )
            }
        }

        composeRule.onNodeWithText(QUICK_CONNECT_POLL_TIMEOUT_MESSAGE).assertIsDisplayed()
        composeRule.onNodeWithTag(QUICK_CONNECT_INITIATE_TEST_TAG).performClick()
        composeRule.runOnIdle { assertTrue(initiated) }
    }

    @Test
    fun quickConnectKeepsWaitingWithoutShowingAnExpiredCountdown() {
        composeRule.setContent {
            MaterialTheme {
                QuickConnectForm(
                    pin = "123456",
                    isAvailable = true,
                    availabilityError = null,
                    isLoading = false,
                    isWaiting = true,
                    secondsRemaining = null,
                    error = null,
                    onInitiate = {},
                    onRetryAvailability = {},
                    onCancel = {},
                    onCopyPin = {},
                )
            }
        }

        composeRule.onNodeWithText("Aguardando autorização...").assertIsDisplayed()
        composeRule.onAllNodesWithText("Expira em 0:00").assertCountEquals(0)
    }

    @Test
    fun serverConfirmedQuickConnectExpirationShowsExpirationMessage() {
        composeRule.setContent {
            MaterialTheme {
                QuickConnectForm(
                    pin = null,
                    isAvailable = true,
                    availabilityError = null,
                    isLoading = false,
                    isWaiting = false,
                    secondsRemaining = null,
                    error = "O código Quick Connect expirou. Gere um novo código.",
                    onInitiate = {},
                    onRetryAvailability = {},
                    onCancel = {},
                    onCopyPin = {},
                )
            }
        }

        composeRule.onNodeWithText("O código Quick Connect expirou. Gere um novo código.").assertIsDisplayed()
    }

    @Test
    fun quickConnectDoesNotOfferInitiationWhileAvailabilityIsUnknown() {
        composeRule.setContent {
            MaterialTheme {
                QuickConnectForm(
                    pin = null,
                    isAvailable = null,
                    availabilityError = null,
                    isLoading = false,
                    isWaiting = false,
                    secondsRemaining = null,
                    error = null,
                    onInitiate = {},
                    onRetryAvailability = {},
                    onCancel = {},
                    onCopyPin = {},
                )
            }
        }

        composeRule.onNodeWithText("Verificando Quick Connect…").assertExists()
        composeRule.onAllNodesWithTag(QUICK_CONNECT_INITIATE_TEST_TAG).assertCountEquals(0)
    }

    @Test
    fun quickConnectAvailabilityFailureOffersRetry() {
        var retried = false

        composeRule.setContent {
            MaterialTheme {
                QuickConnectForm(
                    pin = null,
                    isAvailable = null,
                    availabilityError = "Servidor indisponível",
                    isLoading = false,
                    isWaiting = false,
                    secondsRemaining = null,
                    error = null,
                    onInitiate = {},
                    onRetryAvailability = { retried = true },
                    onCancel = {},
                    onCopyPin = {},
                )
            }
        }

        composeRule.onNodeWithText("Servidor indisponível").assertExists()
        composeRule.onNodeWithTag(QUICK_CONNECT_RETRY_AVAILABILITY_TEST_TAG).performClick()
        composeRule.runOnIdle { assertTrue(retried) }
    }

    @Test
    fun userAvatarExposesAnAccessibleSelectionAction() {
        var selected: AuthUser? = null

        composeRule.setContent {
            MaterialTheme {
                PasswordLoginForm(
                    username = "",
                    password = "",
                    isLoading = false,
                    error = null,
                    onUsernameChange = {},
                    onPasswordChange = {},
                    onLogin = {},
                    users = listOf(AuthUser("u1", "Raphael")),
                    onUserSelect = { selected = it },
                    onRegister = {},
                )
            }
        }

        composeRule.onNodeWithContentDescription("Selecionar Raphael").performClick()

        composeRule.runOnIdle { assertTrue(selected?.name == "Raphael") }
    }
}
