package org.mulletaflix.feature.downloads

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsOff
import androidx.compose.ui.test.assertIsOn
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

class DownloadsBatchSelectionTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun visibleSelectionActionsKeepHiddenSelectionAndRequestDeletionOnlyWhenInvoked() {
        var selectedIds by mutableStateOf(setOf("hidden"))
        var deleteRequested = false
        composeRule.setContent {
            MaterialTheme {
                DownloadSelectionBar(
                    selectedCount = selectedIds.size,
                    visibleCompletedIds = setOf("visible-a", "visible-b"),
                    selectedIds = selectedIds,
                    onSelectionChanged = { selectedIds = it },
                    onDeleteSelected = { deleteRequested = true },
                )
            }
        }

        composeRule.onNodeWithText("Selecionar concluídos exibidos").performClick()
        composeRule.runOnIdle {
            assertEquals(setOf("hidden", "visible-a", "visible-b"), selectedIds)
            assertFalse(deleteRequested)
        }
        composeRule.onNodeWithText("Desmarcar concluídos exibidos").performClick()
        composeRule.runOnIdle { assertEquals(setOf("hidden"), selectedIds) }
        composeRule.onNodeWithText("Excluir selecionados (1)").performClick()
        composeRule.runOnIdle { assertTrue(deleteRequested) }
    }

    @Test
    fun completedRowIsAccessibleAndSelectableByTouch() {
        var selected by mutableStateOf(false)
        composeRule.setContent {
            MaterialTheme {
                DownloadRow(
                    entry = completedEntry("movie", "Filme"),
                    imageModel = null,
                    selectionMode = true,
                    isSelected = selected,
                    onPlay = {},
                    onRetry = {},
                    onRemove = {},
                    onToggleSelected = { selected = !selected },
                )
            }
        }

        val row = composeRule.onNodeWithContentDescription("Selecionar download Filme")
        row.assertIsOff().performClick()
        composeRule.onNodeWithContentDescription("Desmarcar download Filme").assertIsOn()
        composeRule.onNodeWithContentDescription("Desmarcar download Filme")
            .performClick()
        composeRule.onNodeWithContentDescription("Selecionar download Filme").assertIsOff()
    }

    @Test
    fun removingSelectionDialogCanBeCanceledWithoutConfirmingRemoval() {
        var dismissed = false
        var confirmed = false
        composeRule.setContent {
            MaterialTheme {
                SelectedDownloadsRemovalDialog(
                    selectedCount = 2,
                    onDismiss = { dismissed = true },
                    onConfirm = { confirmed = true },
                )
            }
        }

        composeRule.onNodeWithText("Remover 2 download(s) concluído(s) selecionado(s)? Os demais títulos e downloads em andamento serão preservados.")
            .assertExists()
        composeRule.onNodeWithText("Cancelar").performClick()
        composeRule.runOnIdle {
            assertTrue(dismissed)
            assertFalse(confirmed)
        }
    }

    @Test
    fun removingSelectionDialogConfirmsOnlyThroughExplicitAction() {
        var confirmed = false
        composeRule.setContent {
            MaterialTheme {
                SelectedDownloadsRemovalDialog(
                    selectedCount = 2,
                    onDismiss = {},
                    onConfirm = { confirmed = true },
                )
            }
        }

        composeRule.onNodeWithText("Excluir selecionados").performClick()
        composeRule.runOnIdle { assertTrue(confirmed) }
    }

    private fun completedEntry(id: String, title: String) = DownloadEntry(
        id = id,
        title = title,
        uri = "https://server/$id",
        state = DownloadState.Completed,
        percent = 100,
    )

}
