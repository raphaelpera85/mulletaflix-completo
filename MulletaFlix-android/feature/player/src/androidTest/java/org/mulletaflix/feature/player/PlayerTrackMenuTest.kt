package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.junit4.v2.createComposeRule
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class PlayerTrackMenuTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun trackRowsExposeRadioActionAndSelectTheWholeRow() {
        var selectedIndex = -1

        composeRule.setContent {
            MaterialTheme {
                PlayerTrackMenu(
                    title = "Faixa de Áudio",
                    tracks = listOf(
                        TrackInfo(index = 2, displayName = "Português"),
                        TrackInfo(index = 5, displayName = "Inglês"),
                    ),
                    selectedIndex = 0,
                    allowNone = false,
                    onSelect = { selectedIndex = it },
                    onDismiss = {},
                )
            }
        }

        composeRule
            .onNode(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    hasText("Inglês") and
                    hasClickAction(),
            )
            .assertHasClickAction()
            .performClick()

        assertEquals(1, selectedIndex)
    }

    @Test
    fun longTrackListRendersAllOptionsInsideScrollableMenu() {
        composeRule.setContent {
            MaterialTheme {
                PlayerTrackMenu(
                    title = "Legendas",
                    tracks = (0 until 20).map { TrackInfo(index = it, displayName = "Idioma $it") },
                    selectedIndex = -1,
                    onSelect = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onAllNodesWithText("Idioma 19").assertCountEquals(1)
    }

    @Test
    fun longQualityListRendersInsideScrollableMenuAndUsesOneRadioTarget() {
        composeRule.setContent {
            MaterialTheme {
                QualityMenu(
                    qualities = (1..20).map { "Qualidade $it" },
                    selectedQuality = "Qualidade 1",
                    onSelect = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onAllNodesWithText("Qualidade 20").assertCountEquals(1)
        composeRule
            .onNode(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    hasText("Qualidade 20") and
                    hasClickAction(),
            )
            .assertHasClickAction()
    }
}
