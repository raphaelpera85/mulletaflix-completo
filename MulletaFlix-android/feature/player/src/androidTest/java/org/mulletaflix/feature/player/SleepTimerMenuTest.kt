package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.performClick
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
                    remainingMs = null,
                    selectedMinutes = null,
                    isAtMediaEnd = false,
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
                    remainingMs = null,
                    selectedMinutes = null,
                    isAtMediaEnd = false,
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
}
