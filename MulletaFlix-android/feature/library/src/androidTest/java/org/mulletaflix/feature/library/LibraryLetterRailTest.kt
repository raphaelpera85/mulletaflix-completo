package org.mulletaflix.feature.library

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.grid.rememberLazyGridState
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.unit.dp
import org.junit.Rule
import org.junit.Test

class LibraryLetterRailTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun letterJumpIncludesErrorAndFilterHeaders() {
        lateinit var gridState: androidx.compose.foundation.lazy.grid.LazyGridState
        composeRule.setContent {
            MaterialTheme {
                val state = rememberLazyGridState()
                gridState = state
                Box(Modifier.fillMaxSize()) {
                    LazyVerticalGrid(columns = GridCells.Fixed(2), state = state) {
                        item { Text("Erro") }
                        item { Text("Filtros") }
                        items(30) { Text("Mídia $it") }
                    }
                    LibraryLetterRail(
                        targets = listOf(LibraryLetterTarget("C", 20)),
                        hasLoadError = true,
                        hasActiveFilters = true,
                        gridState = state,
                        modifier = Modifier.align(androidx.compose.ui.Alignment.CenterEnd),
                    )
                }
            }
        }

        composeRule.onNodeWithContentDescription("Ir para letra C").performClick()
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Mídia 20").assertIsDisplayed()
    }
}
