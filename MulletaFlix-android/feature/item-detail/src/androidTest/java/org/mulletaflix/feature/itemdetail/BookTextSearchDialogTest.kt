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
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.awaitCancellation
import java.util.concurrent.atomic.AtomicInteger
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
                    search = { query, _ ->
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
                    search = { _, _ -> error("search unavailable") },
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
                    search = { query, _ ->
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
    fun paginatedSearchShowsAccessibleDeterminateProgress() {
        val progressReported = CompletableDeferred<Unit>()
        val finishSearch = CompletableDeferred<List<BookTextSearchResult>>()

        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { _, reportProgress ->
                        reportProgress(3, 10)
                        progressReported.complete(Unit)
                        finishSearch.await()
                    },
                    onDismiss = {},
                    onOpenResult = {},
                )
            }
        }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("termo")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) { progressReported.isCompleted }
        composeRule.onNodeWithText("Analisadas 3 de 10 páginas").assertIsDisplayed()
        composeRule.onNode(
            SemanticsMatcher.expectValue(SemanticsProperties.LiveRegion, LiveRegionMode.Polite) and
                SemanticsMatcher.expectValue(
                    SemanticsProperties.ProgressBarRangeInfo,
                    ProgressBarRangeInfo(3f, 0f..10f),
                ),
        ).assertExists()

        finishSearch.complete(emptyList())
        composeRule.waitForIdle()
    }

    @Test
    fun cancelButtonStopsSearchAndReturnsDialogToIdleState() {
        val searchStarted = CompletableDeferred<Unit>()
        val searchCancelled = CompletableDeferred<Unit>()
        val searchCount = AtomicInteger()

        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { _, _ ->
                        if (searchCount.incrementAndGet() == 1) {
                            searchStarted.complete(Unit)
                            try {
                                awaitCancellation()
                            } finally {
                                searchCancelled.complete(Unit)
                            }
                        } else {
                            listOf(BookTextSearchResult("Resultado depois do cancelamento"))
                        }
                    },
                    onDismiss = {},
                    onOpenResult = {},
                )
            }
        }

        composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("termo")
        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) { searchStarted.isCompleted }

        composeRule.onNodeWithText("Cancelar busca").performClick()
        composeRule.waitUntil(5_000) { searchCancelled.isCompleted }
        composeRule.onNodeWithText("Buscar").assertIsDisplayed()
        composeRule.onAllNodesWithText("Buscando…").fetchSemanticsNodes().also { assertEquals(0, it.size) }
        composeRule.onAllNodesWithText("Cancelar busca").fetchSemanticsNodes().also { assertEquals(0, it.size) }
        composeRule.onNodeWithText("Fechar").assertIsDisplayed()

        composeRule.onNodeWithText("Buscar").performClick()
        composeRule.waitUntil(5_000) {
            composeRule.onAllNodesWithText("Resultado depois do cancelamento").fetchSemanticsNodes().isNotEmpty()
        }
    }

    @Test
    fun editingQueryClearsResultsFromPreviousQuery() {
        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { query, _ -> listOf(BookTextSearchResult("Resultado de $query")) },
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
