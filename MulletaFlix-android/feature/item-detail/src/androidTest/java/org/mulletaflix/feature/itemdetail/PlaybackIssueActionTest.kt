package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class PlaybackIssueActionTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun reportActionNamesTheTitleAndOpensItsFeedbackDialog() {
        var clickCount = 0
        composeRule.setContent {
            MaterialTheme {
                PlaybackIssueAction(itemName = "Filme de teste", onClick = { clickCount++ })
            }
        }

        composeRule.onNodeWithContentDescription("Reportar problema de reprodução de Filme de teste")
            .assertIsDisplayed()
            .performClick()

        assertEquals(1, clickCount)
    }
}
