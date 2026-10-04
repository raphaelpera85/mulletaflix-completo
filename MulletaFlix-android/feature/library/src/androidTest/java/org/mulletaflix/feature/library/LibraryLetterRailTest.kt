package org.mulletaflix.feature.library

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.grid.rememberLazyGridState
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class LibraryLetterRailTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun letterJumpIncludesErrorAndFilterHeaders() {
        lateinit var gridState: androidx.compose.foundation.lazy.grid.LazyGridState
        val items = List(30) {
            MediaItem("item-$it", if (it == 20) "Casa" else "Antigo $it", MediaItemType.Movie)
        }
        var selectedLetter by mutableStateOf<String?>(null)

        composeRule.setContent {
            MaterialTheme {
                val state = rememberLazyGridState()
                gridState = state
                Box(Modifier.fillMaxSize()) {
                    LazyVerticalGrid(columns = GridCells.Fixed(2), state = state) {
                        item { Text("Erro") }
                        item { Text("Filtros") }
                        items(items) { Text(it.name) }
                    }
                    LibraryLetterNavigationEffect(
                        targetLetter = selectedLetter,
                        items = items,
                        hasMore = false,
                        isLoading = false,
                        hasLoadError = true,
                        hasActiveFilters = true,
                        gridState = state,
                        onLoadMore = {},
                        onFinished = { selectedLetter = null },
                    )
                    LibraryLetterRail(
                        targets = listOf(LibraryLetterTarget("C", 20)),
                        loadingLetter = selectedLetter,
                        onTargetSelected = { selectedLetter = it },
                        modifier = Modifier.align(androidx.compose.ui.Alignment.CenterEnd),
                    )
                }
            }
        }

        composeRule.onNodeWithContentDescription("Ir para letra C").performClick()
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Casa").assertIsDisplayed()
    }

    @Test
    fun selectingUnloadedLetterFetchesNextPageThenScrollsToItsFirstTitle() {
        val firstPage = List(35) { MediaItem("a-$it", "Antigo $it", MediaItemType.Movie) }
        var loadedItems by mutableStateOf(firstPage)
        var hasMore by mutableStateOf(true)
        var isLoading by mutableStateOf(false)
        var selectedLetter by mutableStateOf<String?>(null)
        var loadMoreCalls = 0
        var finishedLetter: String? = null

        composeRule.setContent {
            MaterialTheme {
                val state = rememberLazyGridState()
                Box(Modifier.fillMaxSize()) {
                    LazyVerticalGrid(columns = GridCells.Fixed(1), state = state) {
                        items(loadedItems, key = MediaItem::id) { Text(it.name) }
                    }
                    LibraryLetterNavigationEffect(
                        targetLetter = selectedLetter,
                        items = loadedItems,
                        hasMore = hasMore,
                        isLoading = isLoading,
                        hasLoadError = false,
                        hasActiveFilters = false,
                        gridState = state,
                        onLoadMore = {
                            loadMoreCalls++
                            isLoading = true
                            loadedItems = loadedItems + MediaItem("b-1", "Belo título", MediaItemType.Movie)
                            hasMore = false
                            isLoading = false
                        },
                        onFinished = {
                            finishedLetter = it
                            selectedLetter = null
                        },
                    )
                    LibraryLetterRail(
                        targets = libraryLetterTargets(loadedItems, hasMore),
                        loadingLetter = selectedLetter,
                        onTargetSelected = { selectedLetter = it },
                        modifier = Modifier.align(androidx.compose.ui.Alignment.CenterEnd),
                    )
                }
            }
        }

        composeRule.onNodeWithContentDescription("Ir para letra B").performClick()
        composeRule.waitForIdle()

        assert(loadMoreCalls == 1)
        assert(finishedLetter == "B")
        composeRule.onNodeWithText("Belo título").assertIsDisplayed()
    }
}
