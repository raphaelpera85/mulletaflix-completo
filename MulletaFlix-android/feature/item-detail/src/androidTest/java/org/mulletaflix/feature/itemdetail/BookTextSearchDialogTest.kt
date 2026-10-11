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
import androidx.compose.ui.test.performTextReplacement
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.CompletableDeferred
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

    @Test
    fun newerQueryCanReplaceInFlightSearchAndIgnoreLateResults() {
        val initialSearchStarted = CompletableDeferred<Unit>()
        val finishInitialSearch = CompletableDeferred<List<BookTextSearchResult>>()
        val searchedQueries = java.util.Collections.synchronizedList(mutableListOf<String>())

        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { query ->
                        searchedQueries += query
                        if (query == "antigo") {
                            initialSearchStarted.complete(Unit)
                            finishInitialSearch.await()
                        } else {
                            listOf(BookTextSearchResult("Resultado atual: $query"))
                        }
                    },
                    onDismiss = {},
                    onOpenResult = {},
                )
            }
        }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("antigo")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) { initialSearchStarted.isCompleted }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextReplacement("novo")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) { "novo" in searchedQueries }
        composeRule.onNodeWithText("Resultado atual: novo").assertIsDisplayed()

        finishInitialSearch.complete(listOf(BookTextSearchResult("Resultado obsoleto")))
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Resultado atual: novo").assertIsDisplayed()
        assertEquals(0, composeRule.onAllNodesWithText("Resultado obsoleto").fetchSemanticsNodes().size)
    }

    @Test
    fun editingQueryClearsResultsFromPreviousQuery() {
        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { query -> listOf(BookTextSearchResult("Resultado de $query")) },
                    onDismiss = {},
                    onOpenResult = {},
                )
            }
        }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("antigo")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) {
            composeRule.onAllNodesWithText("Resultado de antigo").fetchSemanticsNodes().isNotEmpty()
        }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextReplacement("novo")
        composeRule.waitUntil(5_000) {
            composeRule.onAllNodesWithText("Resultado de antigo").fetchSemanticsNodes().isEmpty()
        }
    }
}
