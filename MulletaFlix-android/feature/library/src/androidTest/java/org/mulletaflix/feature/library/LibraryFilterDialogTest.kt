package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** Regression coverage for remote-friendly library filters on TV and tablet. */
class LibraryFilterDialogTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun exposes_filter_actions_and_clear_action_to_accessibility() {
        val toggled = mutableListOf<String>()
        var cleared = false
        var dismissed = false

        composeRule.setContent {
            MaterialTheme {
                FilterDialog(
                    activeFilters = listOf(LibraryViewModel.FILTER_FAVORITES),
                    onToggle = toggled::add,
                    onClear = { cleared = true },
                    onDismiss = { dismissed = true },
                )
            }
        }

        composeRule.onNodeWithContentDescription("Filtro ${LibraryViewModel.FILTER_FAVORITES}")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithContentDescription("Filtro ${LibraryViewModel.FILTER_UNPLAYED}")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithText("Limpar").performClick()
        composeRule.onNodeWithText("Fechar").performClick()

        composeRule.runOnIdle {
            assertEquals(
                listOf(LibraryViewModel.FILTER_FAVORITES, LibraryViewModel.FILTER_UNPLAYED),
                toggled,
            )
            assertTrue(cleared)
            assertTrue(dismissed)
        }
    }
}
