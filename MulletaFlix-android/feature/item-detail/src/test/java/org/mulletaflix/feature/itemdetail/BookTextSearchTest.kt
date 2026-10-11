package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test

class BookTextSearchTest {
    @Test
    fun `finds matches without case or accent sensitivity`() {
        val chunks = listOf("Primeiro trecho", "Ação começa aqui", "fim das AÇÕES")

        assertEquals(listOf(1), BookTextSearch.search(chunks, "  acao  "))
        assertEquals(listOf(2), BookTextSearch.search(chunks, "acoes"))
    }

    @Test
    fun `returns no matches for blank query and respects result limit`() {
        val chunks = List(BookTextSearch.MAX_RESULTS + 5) { "alvo no trecho $it" }

        assertEquals(emptyList<Int>(), BookTextSearch.search(chunks, "   "))
        assertEquals(
            (0 until BookTextSearch.MAX_RESULTS).toList(),
            BookTextSearch.search(chunks, "alvo"),
        )
    }
}
