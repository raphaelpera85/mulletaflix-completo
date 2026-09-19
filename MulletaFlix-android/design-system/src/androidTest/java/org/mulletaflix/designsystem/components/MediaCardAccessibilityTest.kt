package org.mulletaflix.designsystem.components

import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.junit4.v2.createComposeRule
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.atomic.AtomicBoolean

class MediaCardAccessibilityTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun liveMediaCard_exposesOneActionableAnnouncement() {
        val clicked = AtomicBoolean(false)

        composeRule.setContent {
            MediaCard(
                title = "Canal de notícias",
                imageUrl = null,
                isLive = true,
                onClick = { clicked.set(true) },
            )
        }

        composeRule
            .onNodeWithContentDescription("Abrir Canal de notícias, ao vivo")
            .assertIsDisplayed()
            .assertHasClickAction()
            .performClick()

        assertTrue(clicked.get())
    }
}
