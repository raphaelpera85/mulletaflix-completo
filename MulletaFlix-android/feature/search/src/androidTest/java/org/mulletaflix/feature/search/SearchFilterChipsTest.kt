package org.mulletaflix.feature.search

import android.content.res.Configuration
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.input.key.Key
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue

class SearchFilterChipsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun booksFilterIsVisibleAndSelectsBookType() {
        val activeFilter = mutableStateOf<SearchFilter?>(null)
        composeRule.setContent {
            MaterialTheme {
                SearchFilterChips(
                    activeFilter = activeFilter.value,
                    onFilterSelected = { activeFilter.value = it },
                )
            }
        }

        composeRule.onNodeWithText("Livros").assertIsDisplayed().performClick()
        composeRule.waitForIdle()

        composeRule.runOnIdle { assertEquals(SearchFilter.Books, activeFilter.value) }
        composeRule.onNodeWithText("Livros").assertIsSelected()
    }

    @Test
    fun booksFilterCanBeFocusedAndActivatedWithRemoteSelectKey() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        assumeTrue(
            "Remote-focus behavior is specific to Android TV",
            (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
                Configuration.UI_MODE_TYPE_TELEVISION,
        )
        val activeFilter = mutableStateOf<SearchFilter?>(null)
        composeRule.setContent {
            MaterialTheme {
                Box(Modifier.width(360.dp)) {
                    SearchFilterChips(
                        activeFilter = activeFilter.value,
                        onFilterSelected = { activeFilter.value = it },
                    )
                }
            }
        }

        val filterOrder = listOf(
            "Tudo", "Filmes", "Séries", "Episódios", "Músicas", "Álbuns", "Artistas", "Livros",
        )
        composeRule.onNodeWithText(filterOrder.first()).requestFocus().assertIsFocused()
        filterOrder.zipWithNext().forEach { (current, next) ->
            composeRule.onNodeWithText(current).performKeyInput { pressKey(Key.DirectionRight) }
            composeRule.waitForIdle()
            composeRule.onNodeWithText(next).assertIsFocused().assertIsDisplayed()
        }
        composeRule.onNodeWithText("Livros").performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.waitForIdle()

        composeRule.runOnIdle { assertEquals(SearchFilter.Books, activeFilter.value) }
        composeRule.onNodeWithText("Livros").assertIsSelected()
    }
}
