package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.requestFocus
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class SleepTimerMenuTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun timerOptionIsAWholeRowRadioAction() {
        var selectedMinutes: Int? = null
        var selectedAtMediaEnd = false
        composeRule.setContent {
            MaterialTheme {
                SleepTimerMenu(
                    mode = SleepTimerMode.OFF,
                    remainingMs = null,
                    selectedMinutes = null,
                    onSelect = { selectedMinutes = it },
                    onSelectAtMediaEnd = { selectedAtMediaEnd = true },
                    onDismiss = {},
                )
            }
        }

        composeRule
            .onNode(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    hasText("30 minutos") and
                    hasClickAction(),
            )
            .assertHasClickAction()
            .performClick()

        assertEquals(30, selectedMinutes)
        assertEquals(false, selectedAtMediaEnd)
    }

    @Test
    fun mediaEndOptionIsAWholeRowRadioAction() {
        var selectedAtMediaEnd = false
        composeRule.setContent {
            MaterialTheme {
                SleepTimerMenu(
                    mode = SleepTimerMode.OFF,
                    remainingMs = null,
                    selectedMinutes = null,
                    onSelect = {},
                    onSelectAtMediaEnd = { selectedAtMediaEnd = true },
                    onDismiss = {},
                )
            }
        }

        composeRule
            .onNode(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    hasText("Ao fim da mídia") and
                    hasClickAction(),
            )
            .assertHasClickAction()
            .performClick()

        assertEquals(true, selectedAtMediaEnd)
    }

    /**
     * "Ao fim da mídia" also has no remaining milliseconds, so the "Desativado"
     * row — which asked `remainingMs == null` — was selected at the same time and
     * the menu could not say which timer was armed.
     */
    @Test
    fun onlyOneOptionIsSelectedWhenTheTimerEndsWithTheMedia() {
        composeRule.setContent {
            MaterialTheme {
                SleepTimerMenu(
                    mode = SleepTimerMode.AT_MEDIA_END,
                    remainingMs = null,
                    selectedMinutes = null,
                    onSelect = {},
                    onSelectAtMediaEnd = {},
                    onDismiss = {},
                )
            }
        }

        val selected = composeRule
            .onAllNodes(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    SemanticsMatcher.expectValue(SemanticsProperties.Selected, true),
            )
            .fetchSemanticsNodes()

        assertEquals("exactly one radio row may be selected", 1, selected.size)
    }

    @Test
    fun tvTimerOptionCanReceiveRemoteFocus() {
        assumeTelevisionProfile()
        composeRule.setContent {
            MaterialTheme {
                SleepTimerMenu(
                    mode = SleepTimerMode.OFF,
                    remainingMs = null,
                    selectedMinutes = null,
                    onSelect = {},
                    onSelectAtMediaEnd = {},
                    onDismiss = {},
                )
            }
        }

        val option = composeRule.onNode(
            SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                hasText("30 minutos") and
                hasClickAction(),
        )
        option.requestFocus()
        option.assertIsFocused()
    }
}
