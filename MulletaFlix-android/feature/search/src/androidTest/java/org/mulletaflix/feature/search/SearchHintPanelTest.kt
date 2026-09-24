package org.mulletaflix.feature.search

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.repository.SearchHintItem

@RunWith(AndroidJUnit4::class)
class SearchHintPanelTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun displaysAccessibleHintAndOpensSelectedItem() {
        var selectedId: String? = null
        val hint = SearchHintItem(
            id = "movie-1",
            name = "Matrix",
            type = "Movie",
            year = 1999,
            imageTag = null,
        )

        composeRule.setContent {
            MaterialTheme {
                SearchHintPanel(
                    hints = listOf(hint),
                    isLoading = false,
                    onHintClick = { selectedId = it.id },
                    focusFriendly = false,
                )
            }
        }

        composeRule.onNodeWithText("Matrix").assertIsDisplayed()
        composeRule.onNodeWithText("Movie • 1999").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Abrir sugestão Matrix")
            .performClick()
        assertEquals("movie-1", selectedId)
    }
}
