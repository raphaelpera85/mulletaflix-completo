package org.mulletaflix.feature.search

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Test

class SearchTruncationNoticeTest {

    @Test
    fun `says how many of the total are on screen`() {
        assertEquals(
            "Mostrando 30 de 412 resultados.",
            searchTruncationNotice(shownCount = 30, totalMatching = 412),
        )
    }

    @Test
    fun `does not tell the viewer to refine the search now that loading more exists`() {
        val notice = searchTruncationNotice(shownCount = 30, totalMatching = 412)

        // A frase pedia "refine a busca" quando não havia outro caminho. Com o botão
        // "Carregar mais" logo abaixo, o conselho virou contradição.
        assertFalse(notice!!.contains("refine", ignoreCase = true))
    }

    @Test
    fun `stays silent when the server total equals what arrived`() {
        assertNull(searchTruncationNotice(shownCount = 30, totalMatching = 30))
    }

    @Test
    fun `stays silent when the server did not count`() {
        // Nulo é "não sei". Inventar um total aqui seria pior do que não dizer nada.
        assertNull(searchTruncationNotice(shownCount = 30, totalMatching = null))
    }

    @Test
    fun `stays silent when the total is smaller than the page`() {
        assertNull(searchTruncationNotice(shownCount = 30, totalMatching = 12))
    }
}
