package org.mulletaflix.feature.search

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.layout.padding
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SearchScrollStateRestorationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun searchPositionSurvivesSavedStateRestoration() {
        val restorationTester = StateRestorationTester(composeRule)
        val items = (0 until 60).toList()
        var state: androidx.compose.foundation.lazy.LazyListState? = null

        restorationTester.setContent {
            val listState = rememberSearchScrollState()
            state = listState
            LazyColumn(state = listState, modifier = Modifier.fillMaxSize()) {
                items(items, key = { it }) { Text("Resultado $it", modifier = Modifier.padding(16.dp)) }
            }
        }
        composeRule.runOnIdle { runBlocking { state!!.scrollToItem(30) } }
        composeRule.waitForIdle()
        val positionBeforeRestore = state!!.firstVisibleItemIndex
        assertTrue(positionBeforeRestore > 0)

        restorationTester.emulateSavedInstanceStateRestore()
        composeRule.waitForIdle()
        assertEquals(positionBeforeRestore, state!!.firstVisibleItemIndex)
    }

    @Test
    fun groupedResultCarouselPositionSurvivesSavedStateRestoration() {
        val restorationTester = StateRestorationTester(composeRule)
        val items = (0 until 60).toList()
        var state: androidx.compose.foundation.lazy.LazyListState? = null

        restorationTester.setContent {
            val listState = rememberSearchCarouselScrollState()
            state = listState
            LazyRow(state = listState, modifier = Modifier.fillMaxSize()) {
                items(items, key = { it }) { Text("Resultado $it", modifier = Modifier.padding(16.dp)) }
            }
        }
        composeRule.runOnIdle { runBlocking { state!!.scrollToItem(30) } }
        composeRule.waitForIdle()
        val positionBeforeRestore = state!!.firstVisibleItemIndex
        assertTrue(positionBeforeRestore > 0)

        restorationTester.emulateSavedInstanceStateRestore()
        composeRule.waitForIdle()
        assertEquals(positionBeforeRestore, state!!.firstVisibleItemIndex)
    }
}
