package org.mulletaflix.feature.itemdetail

import android.graphics.Color
import android.graphics.Paint
import android.graphics.pdf.PdfDocument
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runBlocking
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class PdfBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun pdfRendererOpensAndDecodesPagesWithinViewportBounds() = runBlocking(Dispatchers.IO) {
        val file = createPdf()
        try {
            val book = PdfBookDocument.open(file)
            assertEquals(2, book.pageCount)

            val bitmap = book.decodePage(index = 0, maxWidth = 200, maxHeight = 300)
            try {
                assertEquals(200, bitmap.width)
                assertEquals(300, bitmap.height)
            } finally {
                bitmap.recycle()
            }
        } finally {
            file.delete()
        }
    }

    @Test
    fun pdfPageNavigationRendersEveryPageAndRestoresItsLocator() {
        val file = createPdf()
        try {
            val book = PdfBookDocument.open(file)
            var currentPage by mutableIntStateOf(0)
            composeRule.setContent {
                MaterialTheme {
                    Column(Modifier.fillMaxSize()) {
                        PagedBookReaderContent(
                            pageBook = book,
                            currentPage = currentPage,
                            modifier = Modifier.weight(1f),
                        )
                        ComicBookPageControls(
                            currentPage = currentPage,
                            pageCount = book.pageCount,
                            onPageSelected = { currentPage = it },
                        )
                    }
                }
            }

            composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                composeRule.onAllNodesWithContentDescription("Página 1 de 2, ampliação 100%")
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithContentDescription("Página 1 de 2, ampliação 100%").assertIsDisplayed()
            composeRule.onNodeWithContentDescription("Página anterior").assertIsNotEnabled()

            composeRule.onNodeWithContentDescription("Próxima página").performClick()
            composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                composeRule.onAllNodesWithContentDescription("Página 2 de 2, ampliação 100%")
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithContentDescription("Página 2 de 2, ampliação 100%").assertIsDisplayed()
            composeRule.onNodeWithContentDescription("Próxima página").assertIsNotEnabled()

            val savedPage = book.locatorForPage(1)
            assertEquals(1, book.pageIndexFromLocator(savedPage))
        } finally {
            file.delete()
        }
    }

    @Test
    fun pdfReadingProgressPersistsAndRestoresTheSavedPage() = runBlocking(Dispatchers.IO) {
        val file = createPdf()
        val itemId = "pdf-progress-${System.nanoTime()}"
        val scope = HomeFeedCacheScope("pdf-test-server", "https://server.example", "pdf-test-user")
        val book = PdfBookDocument.open(file)
        val store = BookReaderProgressStore(InstrumentationRegistry.getInstrumentation().targetContext)
        val reloadedStore = BookReaderProgressStore(InstrumentationRegistry.getInstrumentation().targetContext)
        try {
            val pageTwo = book.locatorForPage(1)
            store.write(scope, itemId, pageTwo)
            store.addBookmark(scope, itemId, "Página dois", pageTwo)

            val restored = reloadedStore.read(scope, itemId)
            val restoredBookmark = reloadedStore.readBookmarks(scope, itemId)?.single()

            assertEquals(1, book.pageIndexFromLocator(restored))
            assertEquals(1, book.pageIndexFromLocator(restoredBookmark?.locator))
        } finally {
            store.remove(scope, itemId)
            store.removeBookmarks(scope, itemId)
            file.delete()
        }
    }

    @Test
    fun rejectsCorruptPdfInsteadOfShowingAnEmptyReader() {
        val file = File.createTempFile("book-reader-invalid-", ".pdf").apply { writeText("not a PDF") }
        try {
            assertThrows(Exception::class.java) { PdfBookDocument.open(file) }
        } finally {
            file.delete()
        }
    }

    private fun createPdf(): File {
        val file = File.createTempFile("book-reader-test-", ".pdf")
        val document = PdfDocument()
        try {
            repeat(2) { index ->
                val page = document.startPage(PdfDocument.PageInfo.Builder(600, 900, index + 1).create())
                page.canvas.drawColor(if (index == 0) Color.WHITE else Color.LTGRAY)
                page.canvas.drawText(
                    "PDF page ${index + 1}",
                    48f,
                    96f,
                    Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.BLACK; textSize = 32f },
                )
                document.finishPage(page)
            }
            file.outputStream().use(document::writeTo)
        } finally {
            document.close()
        }
        return file
    }
}
