package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class FavoritesCatalogControlsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun searchFieldAcceptsTitleAndShowsAccessibleLabel() {
        var query = ""
        composeRule.setContent {
            MaterialTheme {
                FavoritesCatalogControls(
                    query = query,
                    sortBy = SortOption.Name,
                    sortOrder = SortOrder.Ascending,
                    showSortMenu = false,
                    isOffline = false,
                    onQueryChange = { query = it },
                    onToggleSortMenu = {},
                    onDismissSortMenu = {},
                    onApplySort = { _, _ -> },
                )
            }
        }

        composeRule.onNodeWithContentDescription("Buscar títulos em Minha Lista").assertIsDisplayed()
        composeRule.onNodeWithText("Buscar títulos").performTextInput("Matrix")

        composeRule.runOnIdle { assertEquals("Matrix", query) }
    }

    @Test
    fun sortMenuAppliesDescendingOrder() {
        val menuExpanded = mutableStateOf(false)
        var appliedOrder = SortOrder.Ascending
        composeRule.setContent {
            MaterialTheme {
                FavoritesCatalogControls(
                    query = "",
                    sortBy = SortOption.Name,
                    sortOrder = SortOrder.Ascending,
                    showSortMenu = menuExpanded.value,
                    isOffline = false,
                    onQueryChange = {},
                    onToggleSortMenu = { menuExpanded.value = true },
                    onDismissSortMenu = { menuExpanded.value = false },
                    onApplySort = { _, order ->
                        appliedOrder = order
                        menuExpanded.value = false
                    },
                )
            }
        }

        composeRule.onNodeWithText("Ordenar: Nome · Ascendente").performClick()
        composeRule.onNodeWithContentDescription("Ordem Descendente").performClick()
        composeRule.onNodeWithText("Aplicar").performClick()

        composeRule.runOnIdle { assertEquals(SortOrder.Descending, appliedOrder) }
    }

    @Test
    fun searchAndSortAreDisabledWithoutNetwork() {
        composeRule.setContent {
            MaterialTheme {
                FavoritesCatalogControls(
                    query = "",
                    sortBy = SortOption.Name,
                    sortOrder = SortOrder.Ascending,
                    showSortMenu = false,
                    isOffline = true,
                    onQueryChange = {},
                    onToggleSortMenu = {},
                    onDismissSortMenu = {},
                    onApplySort = { _, _ -> },
                )
            }
        }

        composeRule.onNodeWithText("Buscar títulos").assertIsNotEnabled()
        composeRule.onNodeWithText("Ordenar: Nome · Ascendente").assertIsNotEnabled()
    }
}
