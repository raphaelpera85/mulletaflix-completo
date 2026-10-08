package org.mulletaflix.feature.itemdetail

import java.io.File
import java.io.IOException
import java.util.concurrent.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class CbrBookArchiveTest {
    @Test
    fun `opens CC0 RAR comic and sorts image pages`() {
        val file = CbrTestArchive.writeTo(File.createTempFile("cbr-fixture-", ".cbr"))
        try {
            runBlocking { CbrBookArchive.open(file) }.use { archive ->

                assertEquals(2, archive.pageCount)
                assertEquals("testfile.jpg", archive.pageName(0))
                assertEquals("testfile.png", archive.pageName(1))
                assertEquals(1, archive.pageIndexFromLocator(archive.locatorForPage(1)))
                assertEquals(0, archive.pageIndexFromLocator(archive.locatorForPage(0)))
            }
        } finally {
            file.delete()
        }
    }

    @Test
    fun `decodes a bounded real page without extracting archive entries`() {
        val file = CbrTestArchive.writeTo(File.createTempFile("cbr-decode-", ".cbr"))
        try {
            runBlocking { CbrBookArchive.open(file) }.use { archive ->
                for (index in 0 until archive.pageCount) {
                    val bitmap = archive.decodePage(index, maxWidth = 320, maxHeight = 480)
                    try {
                        assertTrue(bitmap.width in 1..320)
                        assertTrue(bitmap.height in 1..480)
                    } finally {
                        bitmap.recycle()
                    }
                }
            }
        } finally {
            file.delete()
        }
    }

    @Test
    fun `close is idempotent and prevents decoding another page`() {
        val file = CbrTestArchive.writeTo(File.createTempFile("cbr-close-", ".cbr"))
        try {
            val archive = runBlocking { CbrBookArchive.open(file) }

            archive.close()
            archive.close()

            assertTrue(
                runCatching { archive.decodePage(index = 0, maxWidth = 320, maxHeight = 480) }
                    .exceptionOrNull() is IllegalStateException,
            )
        } finally {
            file.delete()
        }
    }

    @Test
    fun `cancelling the indexing job aborts opening`() = runTest {
        val file = CbrTestArchive.writeTo(File.createTempFile("cbr-cancel-", ".cbr"))
        val indexingJob = Job()
        val indexingContext = coroutineContext + indexingJob
        var activeChecks = 0
        try {
            assertThrows(CancellationException::class.java) {
                CbrBookArchive.open(file, indexingContext) {
                    activeChecks++
                    if (activeChecks == 2) indexingJob.cancel()
                }
            }

            assertEquals(2, activeChecks)
        } finally {
            file.delete()
        }
    }

    @Test
    fun `rejects invalid RAR files and page indices`() {
        val invalid = File.createTempFile("cbr-invalid-", ".cbr").apply { writeText("not a RAR archive") }
        try {
            val error = assertThrows(IOException::class.java) { runBlocking { CbrBookArchive.open(invalid) } }
            assertEquals("O arquivo CBR está inválido ou não é compatível.", error.message)
        } finally {
            invalid.delete()
        }

        val empty = File.createTempFile("cbr-empty-", ".cbr")
        try {
            val error = assertThrows(IOException::class.java) { runBlocking { CbrBookArchive.open(empty) } }
            assertEquals("O arquivo CBR está vazio ou não foi encontrado.", error.message)
        } finally {
            empty.delete()
        }

        val valid = CbrTestArchive.writeTo(File.createTempFile("cbr-index-", ".cbr"))
        try {
            runBlocking { CbrBookArchive.open(valid) }.use { archive ->
                assertThrows(IllegalArgumentException::class.java) { archive.locatorForPage(archive.pageCount) }
                assertThrows(IllegalArgumentException::class.java) { archive.decodePage(-1, 320, 480) }
            }
        } finally {
            valid.delete()
        }
    }
}
