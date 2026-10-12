package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.nio.file.Files
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.runTest

class BookTextSearchTest {
    @Test
    fun `search result snippet centers on a late accent-insensitive match`() = runTest {
        val text = "prefácio ".repeat(40) + "Ação estratégica encontrada no capítulo."
        val file = Files.createTempFile("book-search", ".txt").toFile()
        try {
            file.writeText(text)
            val result = PlainTextBookDocument.open(file).searchResults("acao").single()

            assertTrue(result.label.contains("Ação estratégica"))
            assertFalse(result.label.contains(text.take(100)))
            assertTrue(result.label.length <= "Trecho 1: ".length + 140)
            assertEquals(0, result.chunkIndex)
        } finally {
            Files.deleteIfExists(file.toPath())
        }
    }

    @Test
    fun `finds matches without case or accent sensitivity`() = runTest {
        val chunks = listOf("Primeiro trecho", "Ação começa aqui", "fim das AÇÕES")

        assertEquals(listOf(1), BookTextSearch.search(chunks, "  acao  "))
        assertEquals(listOf(2), BookTextSearch.search(chunks, "acoes"))
    }

    @Test
    fun `returns no matches for blank query and respects result limit`() = runTest {
        val chunks = List(BookTextSearch.MAX_RESULTS + 5) { "alvo no trecho $it" }

        assertEquals(emptyList<Int>(), BookTextSearch.search(chunks, "   "))
        assertEquals(
            (0 until BookTextSearch.MAX_RESULTS).toList(),
            BookTextSearch.search(chunks, "alvo"),
        )
    }

    @Test
    fun `stops scanning text chunks promptly when the search coroutine is cancelled`() = runTest {
        val searchJob = Job()
        var readCount = 0
        val chunks = object : AbstractList<String>() {
            override val size: Int = 10_000

            override fun get(index: Int): String {
                readCount++
                if (index == 4) searchJob.cancel()
                return "text without a match"
            }
        }

        val search = CoroutineScope(searchJob).launch {
            BookTextSearch.search(chunks, "target")
        }
        search.join()

        assertTrue(search.isCancelled)
        assertEquals(5, readCount)
    }
}
