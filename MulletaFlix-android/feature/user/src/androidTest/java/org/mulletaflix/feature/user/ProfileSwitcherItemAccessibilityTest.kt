package org.mulletaflix.feature.user

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.getOrNull
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.repository.AvailableUser

@RunWith(AndroidJUnit4::class)
class ProfileSwitcherItemAccessibilityTest {
    @get:Rule
    val composeRule = createComposeRule()

    private val user = AvailableUser(id = "user-2", name = "Raphael")

    @Test
    fun switcherAnnouncesItsPurposeAndDispatchesTheSelectedUser() {
        var selectedUser: AvailableUser? = null
        composeRule.setContent {
            MaterialTheme {
                ProfileSwitcherItem(user = user, avatarUrl = null) { selectedUser = it }
            }
        }

        val switcher = composeRule.onNodeWithText(user.name).assertHasClickAction()
        val semantics = switcher.fetchSemanticsNode().config
        assertEquals(Role.Button, semantics.getOrNull(SemanticsProperties.Role))
        assertEquals(
            "Alternar para ${user.name}",
            semantics.getOrNull(SemanticsActions.OnClick)?.label,
        )

        switcher.performClick()

        assertEquals(user, selectedUser)
    }
}
