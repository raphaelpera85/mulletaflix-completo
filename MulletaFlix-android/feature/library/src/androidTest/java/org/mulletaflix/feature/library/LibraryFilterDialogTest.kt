package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.performTextInput
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
        var cleared = false

        composeRule.setContent {
            MaterialTheme {
                FilterDialog(
                    activeFilters = listOf(LibraryViewModel.FILTER_FAVORITES),
                    onApply = { _, _ -> },
                    onClear = { cleared = true },
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithContentDescription("Filtro ${LibraryViewModel.FILTER_FAVORITES}")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithContentDescription("Filtro ${LibraryViewModel.FILTER_UNPLAYED}")
            .assertIsDisplayed()
            .performClick()
        composeRule.onNodeWithText("Limpar filtros").performClick()

        composeRule.runOnIdle {
            assertTrue(cleared)
        }
    }

    @Test
    fun applies_multiple_server_facets_only_after_confirmation() {
        var applied: Pair<Collection<String>, LibraryFacetFilters>? = null
        var dismissed = false

        composeRule.setContent {
            MaterialTheme {
                FilterDialog(
                    activeFilters = emptyList(),
                    onApply = { filters, facets -> applied = filters to facets },
                    onClear = {},
                    onDismiss = { dismissed = true },
                )
            }
        }

        composeRule.onNodeWithContentDescription("Filtro ${LibraryViewModel.FILTER_FAVORITES}")
            .performClick()
        composeRule.onNodeWithContentDescription("Filtrar por gêneros, separados por vírgula")
            .performTextInput("Drama, Ação")
        composeRule.onNodeWithContentDescription("Filtrar por anos, separados por vírgula")
            .performTextInput("2023, 2024")
        composeRule.onNodeWithContentDescription("Filtrar por classificação indicativa, separados por vírgula")
            .performTextInput("PG-13, TV-MA")

        composeRule.runOnIdle {
            assertEquals(null, applied)
            assertEquals(false, dismissed)
        }
        composeRule.onNodeWithText("Aplicar filtros").performClick()
        composeRule.runOnIdle {
            assertEquals(
                setOf(LibraryViewModel.FILTER_FAVORITES),
                applied?.first?.toSet(),
            )
            assertEquals(
                LibraryFacetFilters("Drama, Ação", "2023, 2024", "PG-13, TV-MA"),
                applied?.second,
            )
        }
    }

    @Test
    fun invalid_year_is_explained_and_prevents_submission() {
        var applied = false
        composeRule.setContent {
            MaterialTheme {
                FilterDialog(
                    activeFilters = emptyList(),
                    onApply = { _, _ -> applied = true },
                    onClear = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithContentDescription("Filtrar por anos, separados por vírgula")
            .performTextInput("20xx")
        composeRule.onNodeWithText("Aplicar filtros").assertIsNotEnabled()
        composeRule.onNodeWithText("Cada ano deve conter quatro dígitos.").assertIsDisplayed()
        composeRule.runOnIdle { assertEquals(false, applied) }
    }

    @Test
    fun cancel_discards_unapplied_changes() {
        var applied = false
        var dismissed = false
        composeRule.setContent {
            MaterialTheme {
                FilterDialog(
                    activeFilters = emptyList(),
                    onApply = { _, _ -> applied = true },
                    onClear = {},
                    onDismiss = { dismissed = true },
                )
            }
        }

        composeRule.onNodeWithContentDescription("Filtrar por gêneros, separados por vírgula")
            .performTextInput("Drama")
        composeRule.onNodeWithText("Cancelar").performClick()
        composeRule.runOnIdle {
            assertEquals(false, applied)
            assertEquals(true, dismissed)
        }
    }

    @Test
    fun dpad_can_focus_and_select_a_library_filter_on_tv() {
        assumeTelevisionProfile()
        composeRule.setContent {
            MaterialTheme {
                FilterDialog(
                    activeFilters = emptyList(),
                    onApply = { _, _ -> },
                    onClear = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithContentDescription("Filtro ${LibraryViewModel.FILTER_FAVORITES}")
            .performKeyInput { pressKey(Key.DirectionCenter) }
            .assertIsSelected()
    }
}
