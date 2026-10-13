package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import java.io.FileOutputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ComicBookOcrIntegrationTest {
    @Test(timeout = 60_000)
    fun cbrPageIsRecognizedAndReturnedAsSearchHit() = runBlocking(Dispatchers.IO) {
        val archiveFile = File.createTempFile("comic-ocr-", ".cbr")
        var archive: CbrBookArchive? = null
        var ocr: ComicBookOcr? = null
        try {
            InstrumentationRegistry.getInstrumentation().context.assets
                .open("books/cbr-ocr-pages.cbr")
                .use { input -> archiveFile.outputStream().use(input::copyTo) }

            val openedArchive = CbrBookArchive.open(archiveFile).also { archive = it }
            val comicOcr = ComicBookOcr(openedArchive).also { ocr = it }
            val extractedPages = mutableListOf<Int>()

            val hits = BookPdfTextSearch.search(openedArchive.pageCount, "page two") { pageIndex ->
                extractedPages += pageIndex
                comicOcr.extractPageText(pageIndex)
            }

            assertEquals(listOf(0, 1), extractedPages)
            assertEquals(1, hits.size)
            assertEquals(1, hits.single().pageIndex)
            assertTrue(hits.single().snippet.contains("PAGE TWO"))
        } finally {
            ocr?.close()
            archive?.close()
            archiveFile.delete()
        }
    }

    @Test(timeout = 60_000)
    fun cbzPageIsRecognizedOfflineAndReturnedAsSearchHit() = runBlocking(Dispatchers.IO) {
        val archiveFile = createScannedComic()
        val archive = ComicBookArchive.open(archiveFile)
        val ocr = ComicBookOcr(archive)
        try {
            val text = ocr.extractPageText(0)
            assertTrue("OCR text was: $text", text.uppercase().contains("MULLETA"))

            val hits = BookPdfTextSearch.search(archive.pageCount, "ocr teste") { page ->
                ocr.extractPageText(page)
            }

            assertEquals(1, hits.size)
            assertEquals(0, hits.single().pageIndex)
        } finally {
            ocr.close()
            archive.close()
            archiveFile.delete()
        }
    }

    private fun createScannedComic(): File {
        val file = File.createTempFile("comic-ocr-", ".cbz")
        val bitmap = Bitmap.createBitmap(1400, 700, Bitmap.Config.ARGB_8888)
        try {
            Canvas(bitmap).apply {
                drawColor(Color.WHITE)
                drawText(
                    "MULLETA OCR TESTE",
                    55f,
                    390f,
                    Paint(Paint.ANTI_ALIAS_FLAG).apply {
                        color = Color.BLACK
                        textSize = 118f
                        typeface = android.graphics.Typeface.create("sans-serif", android.graphics.Typeface.BOLD)
                    },
                )
            }
            ZipOutputStream(FileOutputStream(file)).use { zip ->
                zip.putNextEntry(ZipEntry("page-001.png"))
                check(bitmap.compress(Bitmap.CompressFormat.PNG, 100, zip))
                zip.closeEntry()
            }
        } finally {
            bitmap.recycle()
        }
        return file
    }
}
