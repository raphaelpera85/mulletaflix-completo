package org.mulletaflix.feature.itemdetail

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class BookPdfTextSearchTest {
    @Test
    fun `finds pages without case or accent sensitivity`() = runTest {
        val pages = listOf("capa", "Ação e consequência", "fim")

        val matches = BookPdfTextSearch.search(pages.size, "acao") { pages[it] }

        assertEquals(listOf(1), matches.map { it.pageIndex })
        assertEquals("Ação e consequência", matches.single().snippet)
    }

    @Test
    fun `snippet shows a late accent-insensitive match in the page`() = runTest {
        val page = "Introdução sem o termo. ".repeat(12) + "Ação específica encontrada no final."

        val match = BookPdfTextSearch.search(pageCount = 1, query = "acao especifica") { page }
            .single()

        assertTrue(match.snippet.contains("Ação específica"))
        assertTrue(match.snippet.length <= 140)
    }

    @Test
    fun `snippet stays within the limit when context starts inside a surrogate pair`() = runTest {
        val page = "x" + "🙂".repeat(1_000) + "Ação" + "y".repeat(1_000)

        val snippet = BookPdfTextSearch.search(pageCount = 1, query = "acao") { page }
            .single()
            .snippet

        assertTrue(snippet.contains("Ação"))
        assertTrue(snippet.length <= 140)
        assertTrue(snippet.startsWith("…"))
        assertTrue(snippet.endsWith("…"))
    }

    @Test
    fun `stops extracting after reaching result limit`() = runTest {
        val pages = List(BookTextSearch.MAX_RESULTS + 3) { "alvo na página $it" }
        val extracted = mutableListOf<Int>()

        val matches = BookPdfTextSearch.search(pages.size, "ALVO", maxResults = 2) { index ->
            extracted += index
            pages[index]
        }

        assertEquals(listOf(0, 1), matches.map { it.pageIndex })
        assertEquals(listOf(0, 1), extracted)
    }

    @Test
    fun `blank query does not extract page text`() = runTest {
        val matches = BookPdfTextSearch.search(pageCount = 20, query = "  ") {
            error("Blank query must not read PDF pages")
        }

        assertTrue(matches.isEmpty())
    }

    @Test
    fun `propagates cancellation between page reads`() = runTest {
        val extracted = mutableListOf<Int>()

        try {
            BookPdfTextSearch.search(pageCount = 3, query = "target") { index ->
                extracted += index
                if (index == 1) throw CancellationException("cancel search")
                "no match"
            }
            throw AssertionError("Cancellation must propagate")
        } catch (_: CancellationException) {
            assertEquals(listOf(0, 1), extracted)
        }
    }
}
