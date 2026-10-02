package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BookReaderPolicyTest {

    @Test
    fun `only epub content type is accepted`() {
        assertTrue(isEpubContentType("application/epub+zip"))
        assertTrue(isEpubContentType("application/epub+zip; charset=binary"))
        assertFalse(isEpubContentType("application/pdf"))
        assertFalse(isEpubContentType("application/x-cbz"))
        assertFalse(isEpubContentType("application/octet-stream"))
        assertFalse(isEpubContentType(null))
    }

    @Test
    fun `http errors explain authentication missing book and unsupported format`() {
        assertTrue(bookReaderHttpErrorMessage(401).contains("sessão"))
        assertTrue(bookReaderHttpErrorMessage(403).contains("autorização"))
        assertTrue(bookReaderHttpErrorMessage(404).contains("não foi encontrado"))
        assertTrue(bookReaderHttpErrorMessage(415).contains("formato"))
        assertEquals(
            "Não foi possível baixar o livro (erro 500). Tente novamente.",
            bookReaderHttpErrorMessage(500),
        )
    }
}
