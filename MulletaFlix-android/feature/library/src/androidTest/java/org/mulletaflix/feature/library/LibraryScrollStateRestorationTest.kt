package org.mulletaflix.feature.library

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyGridState
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.foundation.layout.padding
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class LibraryScrollStateRestorationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun gridPositionSurvivesSavedStateRestoration() {
        val restorationTester = StateRestorationTester(composeRule)
        val items = (0 until 60).toList()
        var capturedState: LazyGridState? = null
        var positionBeforeRestore = -1

        restorationTester.setContent {
            val gridState = rememberLibraryGridScrollState()
            capturedState = gridState
            LazyVerticalGrid(
                columns = GridCells.Fixed(3),
                state = gridState,
                modifier = Modifier.fillMaxSize(),
            ) {
                items(items, key = { it }) { Text("Título $it", modifier = Modifier.padding(16.dp)) }
            }
        }
        composeRule.runOnIdle {
            runBlocking { capturedState!!.scrollToItem(30) }
        }
        composeRule.waitForIdle()
        positionBeforeRestore = capturedState!!.firstVisibleItemIndex
        assert(positionBeforeRestore > 0) { "The grid did not move before restoration" }
        restorationTester.emulateSavedInstanceStateRestore()
        composeRule.waitForIdle()
        assertEquals(positionBeforeRestore, capturedState!!.firstVisibleItemIndex)
    }
}
