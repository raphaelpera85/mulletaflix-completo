package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class LibraryOfflineMediaPreviewTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun offlinePreviewShowsCachedMetadataAndMakesStreamingLimitationClear() {
        composeRule.setContent {
            MaterialTheme {
                OfflineMediaPreviewDialog(
                    preview = LibraryOfflineMediaPreview.from(cachedMovie()),
                    isOffline = true,
                    onDismiss = {},
                    onOpenDetails = {},
                )
            }
        }

        composeRule.onNodeWithText("Filme salvo").assertIsDisplayed()
        composeRule.onNodeWithText("Uma sinopse guardada no catálogo.").assertIsDisplayed()
        composeRule.onNodeWithText("Ano: 2024").assertIsDisplayed()
        composeRule.onNodeWithText("Classificação indicativa: 14").assertIsDisplayed()
        composeRule.onNodeWithText("Gêneros: Drama • Ficção científica").assertIsDisplayed()
        composeRule.onNodeWithText(
            "Prévia do catálogo salvo neste dispositivo. Os detalhes completos e a reprodução por streaming precisam de conexão.",
        ).assertIsDisplayed()
        composeRule.onNodeWithText("Fechar").assertIsDisplayed()
    }

    @Test
    fun previewCanOpenFullDetailsAfterConnectionReturns() {
        var opened = false
        composeRule.setContent {
            MaterialTheme {
                OfflineMediaPreviewDialog(
                    preview = LibraryOfflineMediaPreview.from(cachedMovie()),
                    isOffline = false,
                    onDismiss = {},
                    onOpenDetails = { opened = true },
                )
            }
        }

        composeRule.onNodeWithText(
            "Conexão disponível. Abra os detalhes para buscar informações completas; a reprodução por streaming depende do servidor.",
        ).assertIsDisplayed()
        composeRule.onNodeWithText("Abrir detalhes").assertIsDisplayed().performClick()
        composeRule.runOnIdle { assertTrue(opened) }
    }

    @Test
    fun seriesPreviewUsesSavedSeriesDateRange() {
        composeRule.setContent {
            MaterialTheme {
                OfflineMediaPreviewDialog(
                    preview = LibraryOfflineMediaPreview.from(MediaItem(
                        id = "series-1",
                        name = "Série salva",
                        type = MediaItemType.Series,
                        year = 2022,
                        premiereDate = "2022-09-01T00:00:00Z",
                        endDate = "2025-05-01T00:00:00Z",
                    )),
                    isOffline = true,
                    onDismiss = {},
                    onOpenDetails = {},
                )
            }
        }

        composeRule.onNodeWithText("Ano: 2022 - 2025").assertIsDisplayed()
    }

    @Test
    fun selectedPreviewSurvivesActivityStateRestorationWithoutTheCatalogPage() {
        val restorationTester = StateRestorationTester(composeRule)
        restorationTester.setContent {
            val preview = rememberSaveable(stateSaver = LibraryOfflineMediaPreviewSaver) {
                mutableStateOf(LibraryOfflineMediaPreview.None)
            }
            LaunchedEffect(Unit) {
                if (preview.value.id.isBlank()) {
                    preview.value = LibraryOfflineMediaPreview.from(cachedMovie())
                }
            }
            MaterialTheme {
                if (preview.value.id.isNotBlank()) {
                    OfflineMediaPreviewDialog(
                        preview = preview.value,
                        isOffline = true,
                        onDismiss = { preview.value = LibraryOfflineMediaPreview.None },
                        onOpenDetails = {},
                    )
                }
            }
        }

        composeRule.waitForIdle()
        restorationTester.emulateSavedInstanceStateRestore()

        composeRule.onNodeWithText("Filme salvo").assertIsDisplayed()
        composeRule.onNodeWithText("Ano: 2024").assertIsDisplayed()
    }

    private fun cachedMovie() = MediaItem(
        id = "movie-1",
        name = "Filme salvo",
        type = MediaItemType.Movie,
        overview = "Uma sinopse guardada no catálogo.",
        year = 2024,
        officialRating = "14",
        genres = listOf("Drama", "Ficção científica"),
    )
}
