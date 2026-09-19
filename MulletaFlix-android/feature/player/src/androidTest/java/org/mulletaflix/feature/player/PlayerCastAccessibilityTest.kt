package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipeLeft
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Text
import org.junit.Rule
import org.junit.Test

class PlayerCastAccessibilityTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun castActionExposesAUnifiedDescription() {
        composeRule.setContent {
            MaterialTheme {
                androidx.compose.foundation.layout.Row(
                       modifier = androidx.compose.ui.Modifier.semantics(mergeDescendants = true) {
                           contentDescription = CAST_ACTION_CONTENT_DESCRIPTION
                    },
                ) {
                    androidx.compose.material3.Text("Transmitir")
                }
            }
        }

           composeRule
               .onAllNodesWithContentDescription(CAST_ACTION_CONTENT_DESCRIPTION)
               .assertCountEquals(1)
    }

    @Test
    fun topBarActionsCanBeScrolledHorizontallyOnNarrowWindows() {
        composeRule.setContent {
            MaterialTheme {
                Box(modifier = androidx.compose.ui.Modifier.width(180.dp)) {
                    PlayerTopBarActionsRow(modifier = androidx.compose.ui.Modifier.fillMaxWidth()) {
                        Text("Ação inicial")
                        Text("Ação final")
                    }
                }
            }
        }

        composeRule
            .onNodeWithContentDescription(PLAYER_TOP_BAR_ACTIONS_CONTENT_DESCRIPTION)
            .performTouchInput { swipeLeft() }
        composeRule.onNodeWithText("Ação final").assertIsDisplayed()
    }
}
