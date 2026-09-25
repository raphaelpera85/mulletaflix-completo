package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

/** Regression coverage for the sort direction controls used by phone, tablet and TV. */
class LibrarySortDropdownTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun exposesAscendingAndDescendingAndAppliesBothSelectionsTogether() {
        var selectedSort: SortOption? = null
        var selectedOrder: SortOrder? = null

        composeRule.setContent {
            SortMenuTestSurface(onApply = { option, order ->
                selectedSort = option
                selectedOrder = order
            })
        }

        composeRule.onNodeWithText("Ascendente").assertIsDisplayed()
        composeRule.onNodeWithText("Descendente").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Ordem Ascendente").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Ordem Descendente").assertIsDisplayed()
        composeRule.onNodeWithText("Data de Lançamento").performClick()
        composeRule.onNodeWithText("Descendente").performClick()
        composeRule.onNodeWithText("Aplicar").performClick()

        composeRule.runOnIdle {
            assertEquals(SortOption.ReleaseDate, selectedSort)
            assertEquals(SortOrder.Descending, selectedOrder)
        }
    }
}

@Composable
private fun SortMenuTestSurface(onApply: (SortOption, SortOrder) -> Unit) {
    MaterialTheme {
        SortDropdown(
            current = SortOption.Name,
            currentOrder = SortOrder.Ascending,
            onApply = onApply,
            onDismiss = {},
        )
    }
}
