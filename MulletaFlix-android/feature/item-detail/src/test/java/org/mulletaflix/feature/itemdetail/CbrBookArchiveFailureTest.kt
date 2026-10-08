package org.mulletaflix.feature.itemdetail

import java.io.File
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Test

class CbrBookArchiveFailureTest {
    @Test
    fun `invalid RAR reports a localized recoverable message`() = runBlocking {
        val file = File.createTempFile("invalid-book-", ".cbr").apply { writeText("not a RAR archive") }
        try {
            val error = runCatching { CbrBookArchive.open(file) }.exceptionOrNull()
            assertEquals("O arquivo CBR está inválido ou não é compatível.", error?.message)
        } finally {
            file.delete()
        }
    }

    @Test
    fun `empty CBR reports a localized error`() = runBlocking {
        val file = File.createTempFile("empty-book-", ".cbr")
        try {
            val error = runCatching { CbrBookArchive.open(file) }.exceptionOrNull()
            assertEquals("O arquivo CBR está vazio ou não foi encontrado.", error?.message)
        } finally {
            file.delete()
        }
    }
}
