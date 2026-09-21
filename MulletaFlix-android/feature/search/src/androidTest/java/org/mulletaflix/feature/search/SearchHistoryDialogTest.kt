package org.mulletaflix.feature.search

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import org.junit.Rule
import org.junit.Test

class SearchHistoryDialogTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun clearHistoryDialogExplainsDestructiveActionAndOffersCancel() {
        composeRule.setContent {
            MaterialTheme {
                ClearSearchHistoryDialog(onConfirm = {}, onDismiss = {})
            }
        }

        composeRule.onNodeWithText("Limpar histórico?").assertIsDisplayed()
        composeRule.onNodeWithText("Todas as buscas recentes serão removidas deste usuário.").assertIsDisplayed()
        composeRule.onNodeWithText("Cancelar").performClick()
    }

    @Test
    fun confirmActionIsExposed() {
        var confirmed = false
        composeRule.setContent {
            MaterialTheme {
                ClearSearchHistoryDialog(onConfirm = { confirmed = true }, onDismiss = {})
            }
        }

        composeRule.onNodeWithText("Limpar").performClick()
        check(confirmed)
    }

    @Test
    fun televisionHistoryEntryIsAReplayFocusTarget() {
        var replayed = false
        composeRule.setContent {
            MaterialTheme {
                SearchHistory(
                    history = listOf("Matrix"),
                    onItemClick = { replayed = true },
                    onRemoveItem = {},
                    onClearHistory = {},
                    focusFriendly = true,
                )
            }
        }

        composeRule.onNodeWithContentDescription("Pesquisar novamente por Matrix").performClick()
        check(replayed)
    }
}
