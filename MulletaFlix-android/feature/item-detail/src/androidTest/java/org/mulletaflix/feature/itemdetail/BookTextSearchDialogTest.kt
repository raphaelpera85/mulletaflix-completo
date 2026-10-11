package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class BookTextSearchDialogTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun searchResultOpensMatchingChunk() {
        var openedChunk: Int? = null
        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { query ->
                        listOf("Introdução", "Ação e consequência", "Conclusão")
                            .let { chunks -> BookTextSearch.search(chunks, query).map { index ->
                                BookTextSearchResult("Trecho ${index + 1}: ${chunks[index]}", chunkIndex = index)
                            } }
                    },
                    onDismiss = {},
                    onOpenResult = { openedChunk = it.chunkIndex },
                )
            }
        }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("acao")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) {
            composeRule.onAllNodesWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithTag("$BOOK_TEXT_SEARCH_RESULT_TEST_TAG-0").assertIsDisplayed().performClick()

        composeRule.runOnIdle { assertEquals(1, openedChunk) }
    }

    @Test
    fun searchFailureShowsRecoverableMessage() {
        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { error("search unavailable") },
                    onDismiss = {},
                    onOpenResult = {},
                )
            }
        }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("termo")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) {
            composeRule.onAllNodesWithText("Não foi possível pesquisar este livro.")
                .fetchSemanticsNodes().isNotEmpty()
        }
    }
}
