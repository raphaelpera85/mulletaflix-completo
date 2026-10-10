package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextClearance
import androidx.compose.ui.test.performTextInput
import androidx.compose.ui.test.performTouchInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.io.File
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ComicBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun decodesOnlyToTheAvailablePageBounds() = runBlocking(Dispatchers.IO) {
        val file = createComicArchive()
        try {
            val archive = ComicBookArchive.open(file)
            val bitmap = archive.decodePage(index = 0, maxWidth = 200, maxHeight = 300)

            assertEquals(200, bitmap.width)
            assertEquals(300, bitmap.height)
            bitmap.recycle()
        } finally {
            file.delete()
        }
    }

    @Test
    fun pageControlsNavigateAndStopAtBothArchiveBoundaries() {
        val file = createComicArchive()
        try {
            val archive = ComicBookArchive.open(file)
            var currentPage by mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    Column(Modifier.fillMaxSize()) {
                        PagedBookReaderContent(
                            pageBook = archive,
                            currentPage = currentPage,
                            modifier = Modifier.weight(1f),
                        )
                        ComicBookPageControls(
                            currentPage = currentPage,
                            pageCount = archive.pageCount,
                            onPageSelected = { currentPage = it },
                        )
                    }
                }
            }

            composeRule.onNodeWithContentDescription("Página anterior").assertIsNotEnabled()
            composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                composeRule.onAllNodesWithContentDescription("Página 1 de 2", substring = true)
                    .fetchSemanticsNodes().isNotEmpty()
            }

            composeRule.onNodeWithContentDescription("Próxima página").performClick()
            composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                composeRule.onAllNodesWithContentDescription("Página 2 de 2", substring = true)
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithContentDescription("Próxima página").assertIsNotEnabled()

            composeRule.onNodeWithContentDescription("Página anterior").performClick()
            composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()
            composeRule.onNodeWithContentDescription("Página anterior").assertIsNotEnabled()
        } finally {
            file.delete()
        }
    }

    @Test
    fun canJumpDirectlyToASelectedPage() {
        val file = createComicArchive(pageCount = 4)
        try {
            val archive = ComicBookArchive.open(file)
            var currentPage by mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    Column(Modifier.fillMaxSize()) {
                        PagedBookReaderContent(
                            pageBook = archive,
                            currentPage = currentPage,
                            modifier = Modifier.weight(1f),
                        )
                        ComicBookPageControls(
                            currentPage = currentPage,
                            pageCount = archive.pageCount,
                            onPageSelected = { currentPage = it },
                        )
                    }
                }
            }

            composeRule.onNodeWithContentDescription("Ir para página", substring = true).performClick()
            composeRule.onNodeWithTag("page-number-input").performTextClearance()
            composeRule.onNodeWithTag("page-number-input").performTextInput("3")
            composeRule.onNodeWithText("Ir").performClick()

            composeRule.onNodeWithText("Página 3 de 4").assertIsDisplayed()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                composeRule.onAllNodesWithContentDescription("Página 3 de 4", substring = true)
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.runOnIdle { assertEquals(2, currentPage) }
        } finally {
            file.delete()
        }
    }

    @Test
    fun pageJumpRejectsNumbersOutsideTheBookAndCanBeCancelled() {
        val file = createComicArchive()
        try {
            val archive = ComicBookArchive.open(file)
            var currentPage by mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    Column(Modifier.fillMaxSize()) {
                        PagedBookReaderContent(
                            pageBook = archive,
                            currentPage = currentPage,
                            modifier = Modifier.weight(1f),
                        )
                        ComicBookPageControls(
                            currentPage = currentPage,
                            pageCount = archive.pageCount,
                            onPageSelected = { currentPage = it },
                        )
                    }
                }
            }

            composeRule.onNodeWithContentDescription("Ir para página", substring = true).performClick()
            composeRule.onNodeWithTag("page-number-input").performTextClearance()
            composeRule.onNodeWithTag("page-number-input").performTextInput("3")

            composeRule.onNodeWithText("Informe um número entre 1 e 2").assertIsDisplayed()
            composeRule.onNodeWithText("Ir").assertIsNotEnabled()
            composeRule.onNodeWithText("Cancelar").performClick()
            composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()
            composeRule.runOnIdle { assertEquals(0, currentPage) }
        } finally {
            file.delete()
        }
    }

    @Test
    fun comicPagesCanBeMagnifiedWithAccessibleControls() {
        val file = createComicArchive()
        try {
            val archive = ComicBookArchive.open(file)
            var zoom by mutableFloatStateOf(1f)
            var currentPage by mutableIntStateOf(0)

            composeRule.setContent {
                MaterialTheme {
                    LaunchedEffect(currentPage) { zoom = 1f }
                    Scaffold(
                        topBar = {
                            ComicBookZoomControls(zoom = zoom, onZoomChange = { zoom = it })
                        },
                        bottomBar = {
                            ComicBookPageControls(
                                currentPage = currentPage,
                                pageCount = archive.pageCount,
                                onPageSelected = { currentPage = it },
                            )
                        },
                    ) { padding ->
                        PagedBookReaderContent(
                            pageBook = archive,
                            currentPage = currentPage,
                            zoom = zoom,
                            onZoomChange = { zoom = it },
                            modifier = Modifier.fillMaxSize().padding(padding),
                        )
                    }
                }
            }

            composeRule.onNodeWithContentDescription("Reduzir ampliação").assertIsNotEnabled()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                composeRule.onAllNodesWithContentDescription("Página 1 de 2, ampliação 100%")
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithContentDescription("Ampliar página").performClick()
            composeRule.onNodeWithText("150%").assertIsDisplayed()
            composeRule.onNodeWithContentDescription("Página 1 de 2, ampliação 150%").assertIsDisplayed()
            composeRule.onNodeWithContentDescription("Reduzir ampliação").assertIsEnabled()
            composeRule.onNodeWithContentDescription("Reduzir ampliação").performClick()
            composeRule.runOnIdle { assertEquals(1f, zoom) }
            composeRule.onNodeWithText("100%").assertIsDisplayed()
            composeRule.onNodeWithContentDescription("Página 1 de 2, ampliação 100%").assertIsDisplayed()

            composeRule.onNodeWithTag("comic-book-page").performTouchInput {
                val pinchCenter = center
                down(0, pinchCenter - Offset(40f, 0f))
                down(1, pinchCenter + Offset(40f, 0f))
                moveBy(0, Offset(-45f, 0f), delayMillis = 100)
                moveBy(1, Offset(45f, 0f), delayMillis = 100)
                up(0)
                up(1)
            }
            composeRule.runOnIdle { org.junit.Assert.assertTrue("Pinch should enlarge the page", zoom > 1f) }
            composeRule.onNodeWithTag("comic-book-page").performTouchInput {
                down(0, center)
                moveBy(0, Offset(40f, 30f), delayMillis = 100)
                up(0)
            }
            composeRule.onNodeWithContentDescription("Próxima página").performClick()
            composeRule.runOnIdle { assertEquals(1f, zoom) }
            composeRule.waitUntil(timeoutMillis = 10_000) {
                composeRule.onAllNodesWithContentDescription("Página 2 de 2, ampliação 100%")
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithContentDescription("Página 2 de 2, ampliação 100%").assertIsDisplayed()
        } finally {
            file.delete()
        }
    }

    private fun createComicArchive(pageCount: Int = 2): File {
        val file = File.createTempFile("comic-reader-", ".cbz")
        ZipOutputStream(file.outputStream()).use { archive ->
            val pageNames = if (pageCount == 2) {
                listOf("pages/10.png", "pages/2.png")
            } else {
                (1..pageCount).map { pageNumber -> "pages/$pageNumber.png" }
            }
            pageNames.forEachIndexed { index, name ->
                val bitmap = Bitmap.createBitmap(1_600, 2_400, Bitmap.Config.ARGB_8888)
                try {
                    bitmap.eraseColor(if (index == 0) android.graphics.Color.RED else android.graphics.Color.BLUE)
                    archive.putNextEntry(ZipEntry(name))
                    check(bitmap.compress(Bitmap.CompressFormat.PNG, 100, archive))
                    archive.closeEntry()
                } finally {
                    bitmap.recycle()
                }
            }
        }
        return file
    }
}
