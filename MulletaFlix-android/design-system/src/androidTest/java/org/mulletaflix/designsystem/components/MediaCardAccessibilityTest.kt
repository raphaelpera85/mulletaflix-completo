package org.mulletaflix.designsystem.components

import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.runtime.remember
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

    @Test
    fun remoteFriendlyMediaCard_acceptsRemoteFocus() {
        val focusRequester = FocusRequester()

        composeRule.setContent {
            MediaCard(
                title = "Filme na TV",
                imageUrl = null,
                focusFriendly = true,
                onClick = {},
                modifier = androidx.compose.ui.Modifier.focusRequester(focusRequester),
            )
        }

        composeRule.runOnIdle { focusRequester.requestFocus() }
        composeRule.onNodeWithContentDescription("Abrir Filme na TV").assertIsFocused()
    }
}
