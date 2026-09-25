package org.mulletaflix.feature.user

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.repository.AvailableUser

@RunWith(AndroidJUnit4::class)
class ProfileSwitcherItemRemoteFocusTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun switcherCanReceiveRemoteKeyboardFocus() {
        val user = AvailableUser(id = "user-2", name = "Raphael")
        val focusRequester = FocusRequester()
        composeRule.setContent {
            MaterialTheme {
                ProfileSwitcherItem(
                    user = user,
                    avatarUrl = null,
                    modifier = Modifier.focusRequester(focusRequester),
                    onClick = {},
                )
            }
        }

        composeRule.runOnIdle { assertTrue(focusRequester.requestFocus()) }

        composeRule.onNodeWithText(user.name).assertIsFocused()
    }
}
