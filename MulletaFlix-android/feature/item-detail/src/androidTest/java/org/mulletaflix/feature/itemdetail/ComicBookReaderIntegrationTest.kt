package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
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
                        ComicBookReaderContent(
                            archive = archive,
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
    fun comicPagesCanBeMagnifiedWithAccessibleControls() {
        val file = createComicArchive()
        try {
            val archive = ComicBookArchive.open(file)
            var zoom by mutableFloatStateOf(1f)

            composeRule.setContent {
                MaterialTheme {
                    Scaffold(
                        topBar = {
                            ComicBookZoomControls(zoom = zoom, onZoomChange = { zoom = it })
                        },
                    ) { padding ->
                        ComicBookReaderContent(
                            archive = archive,
                            currentPage = 0,
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
        } finally {
            file.delete()
        }
    }

    private fun createComicArchive(): File {
        val file = File.createTempFile("comic-reader-", ".cbz")
        ZipOutputStream(file.outputStream()).use { archive ->
            listOf("pages/10.png", "pages/2.png").forEachIndexed { index, name ->
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
