package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.pdf.PdfDocument
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
import com.google.android.gms.tasks.Task
import com.google.android.gms.tasks.TaskCompletionSource
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.awaitCancellation
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.json.JSONObject
import org.readium.r2.shared.publication.Locator

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
    fun cancelingComicOcrSearchReturnsToIdleAndIgnoresLateRecognitionBeforeRetry() {
        val source = ControlledComicPageSource()
        val recognizer = ControlledComicPageTextRecognizer()
        val ocr = ComicBookOcr(source, ComicPageTextRecognizerFactory { recognizer })
        val oldSearchCancelled = CompletableDeferred<Unit>()
        val searchCount = AtomicInteger()

        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { query, reportProgress ->
                        val searchNumber = searchCount.incrementAndGet()
                        try {
                            BookPdfTextSearch.search(
                                pageCount = source.pageCount,
                                query = query,
                                onProgress = reportProgress,
                                extractPageText = ocr::extractPageText,
                            ).map { hit ->
                                BookTextSearchResult("Resultado ${searchNumber}: ${hit.snippet}", hit.pageIndex)
                            }
                        } finally {
                            if (searchNumber == 1) oldSearchCancelled.complete(Unit)
                        }
                    },
                    onDismiss = {},
                    onOpenResult = {},
                )
            }
        }

        try {
            composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("antigo")
            composeRule.onNodeWithText("Buscar").performClick()
            assertTrue("A primeira consulta deve iniciar reconhecimento OCR.", recognizer.firstStarted.await(5, TimeUnit.SECONDS))

            composeRule.onNodeWithText("Cancelar busca").performClick()
            composeRule.waitUntil(5_000) { oldSearchCancelled.isCompleted }
            composeRule.onNodeWithText("Buscar").assertIsDisplayed()
            assertEquals(0, composeRule.onAllNodesWithText("Cancelar busca").fetchSemanticsNodes().size)
            assertEquals(0, composeRule.onAllNodesWithText("Buscando…").fetchSemanticsNodes().size)
            assertTrue("Bitmap permanece válido enquanto ML Kit ainda lê a página.", !source.bitmaps.single().isRecycled)

            composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextReplacement("novo")
            composeRule.onNodeWithText("Buscar").performClick()
            composeRule.waitUntil(5_000) { searchCount.get() == 2 }
            assertEquals("A segunda consulta espera a tarefa OCR anterior liberar o semáforo.", 1, recognizer.tasks.size)

            recognizer.tasks[0].setResult("Conteúdo antigo obsoleto")
            assertTrue("A nova consulta deve iniciar depois da limpeza do primeiro OCR.", recognizer.secondStarted.await(5, TimeUnit.SECONDS))
            composeRule.waitUntil(5_000) { source.bitmaps.first().isRecycled }
            assertEquals(2, recognizer.tasks.size)
            assertEquals(2, source.bitmaps.size)
            assertTrue("A tarefa atual deve manter seu próprio bitmap vivo.", !source.bitmaps[1].isRecycled)
            assertEquals(0, composeRule.onAllNodesWithText("Resultado 1: Conteúdo antigo obsoleto").fetchSemanticsNodes().size)

            recognizer.tasks[1].setResult("Conteúdo novo atualizado")
            composeRule.waitUntil(5_000) {
                composeRule.onAllNodesWithText("Resultado 2: Conteúdo novo atualizado").fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithText("Resultado 2: Conteúdo novo atualizado").assertIsDisplayed()
            assertTrue(source.bitmaps[1].isRecycled)
            assertEquals(0, composeRule.onAllNodesWithText("Resultado 1: Conteúdo antigo obsoleto").fetchSemanticsNodes().size)
        } finally {
            ocr.close()
            recognizer.tasks.forEach { it.trySetResult("cleanup") }
            composeRule.waitForIdle()
            source.bitmaps.forEach { if (!it.isRecycled) it.recycle() }
        }
    }

    @Test
    fun cancelingPdfOcrSearchReturnsToIdleAndIgnoresLateRecognitionBeforeRetry() {
        val file = createBlankPdf()
        val bitmaps = CopyOnWriteArrayList<Bitmap>()
        val recognizer = ControlledPdfPageTextRecognizer()
        val document = PdfBookDocument(
            file = file,
            pageCount = 1,
            recognizerFactory = PdfPageTextRecognizerFactory { recognizer },
            pageBitmapDecoder = { _, _, _ ->
                Bitmap.createBitmap(32, 32, Bitmap.Config.ARGB_8888).also(bitmaps::add)
            },
        )
        val oldSearchCancelled = CompletableDeferred<Unit>()
        val searchCount = AtomicInteger()

        composeRule.setContent {
            MaterialTheme {
                BookTextSearchDialog(
                    search = { query, reportProgress ->
                        val searchNumber = searchCount.incrementAndGet()
                        try {
                            BookPdfTextSearch.search(
                                pageCount = document.pageCount,
                                query = query,
                                onProgress = reportProgress,
                                extractPageText = document::extractPageText,
                            ).map { hit ->
                                BookTextSearchResult(
                                    "Resultado ${searchNumber}: ${hit.snippet}",
                                    hit.pageIndex,
                                )
                            }
                        } finally {
                            if (searchNumber == 1) oldSearchCancelled.complete(Unit)
                        }
                    },
                    onDismiss = {},
                    onOpenResult = {},
                )
            }
        }

        try {
            composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextInput("antigo")
            composeRule.onNodeWithText("Buscar").performClick()
            assertTrue(
                "A primeira consulta deve iniciar reconhecimento OCR do PDF.",
                recognizer.firstStarted.await(20, TimeUnit.SECONDS),
            )

            composeRule.onNodeWithText("Cancelar busca").performClick()
            composeRule.waitUntil(5_000) { oldSearchCancelled.isCompleted }
            composeRule.onNodeWithText("Buscar").assertIsDisplayed()
            assertEquals(0, composeRule.onAllNodesWithText("Cancelar busca").fetchSemanticsNodes().size)
            assertEquals(0, composeRule.onAllNodesWithText("Buscando…").fetchSemanticsNodes().size)
            assertTrue("Bitmap permanece válido enquanto ML Kit ainda lê a página.", !bitmaps.single().isRecycled)

            composeRule.onNodeWithTag(BOOK_TEXT_SEARCH_QUERY_TEST_TAG).performTextReplacement("novo")
            composeRule.onNodeWithText("Buscar").performClick()
            composeRule.waitUntil(5_000) { searchCount.get() == 2 }
            assertEquals("A consulta seguinte aguarda a Task OCR anterior liberar o semáforo.", 1, recognizer.tasks.size)

            recognizer.tasks[0].setResult("Conteúdo antigo obsoleto")
            assertTrue(
                "A nova consulta inicia após a limpeza do OCR anterior.",
                recognizer.secondStarted.await(20, TimeUnit.SECONDS),
            )
            composeRule.waitUntil(5_000) { bitmaps.first().isRecycled }
            assertEquals(2, recognizer.tasks.size)
            assertEquals(2, bitmaps.size)
            assertTrue("A Task atual deve manter seu próprio bitmap vivo.", !bitmaps[1].isRecycled)
            assertEquals(0, composeRule.onAllNodesWithText("Resultado 1: Conteúdo antigo obsoleto").fetchSemanticsNodes().size)

            recognizer.tasks[1].setResult("Conteúdo novo atualizado")
            composeRule.waitUntil(5_000) {
                composeRule.onAllNodesWithText("Resultado 2: Conteúdo novo atualizado").fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithText("Resultado 2: Conteúdo novo atualizado").assertIsDisplayed()
            assertTrue(bitmaps[1].isRecycled)
            assertEquals(0, composeRule.onAllNodesWithText("Resultado 1: Conteúdo antigo obsoleto").fetchSemanticsNodes().size)
        } finally {
            document.close()
            recognizer.tasks.forEach { it.trySetResult("cleanup") }
            composeRule.waitForIdle()
            bitmaps.forEach { if (!it.isRecycled) it.recycle() }
            file.delete()
        }
    }

    private fun createBlankPdf(): File {
        val file = File.createTempFile("book-search-ocr-", ".pdf")
        val document = PdfDocument()
        try {
            val page = document.startPage(PdfDocument.PageInfo.Builder(120, 120, 1).create())
            page.canvas.drawColor(Color.WHITE)
            document.finishPage(page)
            file.outputStream().use(document::writeTo)
        } finally {
            document.close()
        }
        return file
    }

    private class ControlledPdfPageTextRecognizer : PdfPageTextRecognizer {
        val tasks = CopyOnWriteArrayList<TaskCompletionSource<String>>()
        val firstStarted = CountDownLatch(1)
        val secondStarted = CountDownLatch(1)

        override fun process(bitmap: Bitmap): Task<String> = TaskCompletionSource<String>().also { task ->
            tasks += task
            if (tasks.size == 1) firstStarted.countDown() else secondStarted.countDown()
        }.task

        override fun close() = Unit
    }

    private class ControlledComicPageTextRecognizer : ComicPageTextRecognizer {
        val tasks = CopyOnWriteArrayList<TaskCompletionSource<String>>()
        val firstStarted = CountDownLatch(1)
        val secondStarted = CountDownLatch(1)

        override fun process(bitmap: Bitmap): Task<String> = TaskCompletionSource<String>().also { task ->
            tasks += task
            if (tasks.size == 1) firstStarted.countDown() else secondStarted.countDown()
        }.task

        override fun close() = Unit
    }

    private class ControlledComicPageSource : BookPageSource {
        override val pageCount = 1
        val bitmaps = CopyOnWriteArrayList<Bitmap>()

        override fun locatorForPage(index: Int) =
            requireNotNull(Locator.fromJSON(JSONObject("""{"href":"page-$index"}""")))

        override fun pageIndexFromLocator(locator: Locator?) = null

        override fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap =
            Bitmap.createBitmap(8, 8, Bitmap.Config.ARGB_8888).also { bitmaps += it }
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
