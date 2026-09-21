package org.mulletaflix.designsystem.components

import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.getOrNull
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.runtime.remember
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.assertFalse
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

    @Test
    fun nonClickableMediaCard_doesNotExposeNestedClickAction() {
        composeRule.setContent {
            MediaCard(
                title = "Linha de biblioteca",
                imageUrl = null,
                isClickable = false,
            )
        }

        val semantics = composeRule
            .onNodeWithContentDescription("Abrir Linha de biblioteca")
            .fetchSemanticsNode()
            .config
        assertFalse(semantics.contains(SemanticsActions.OnClick))
    }

    @Test
    fun mediaCardMergesIntoASingleAccessibleNode() {
        // TalkBack focus order depends on this: the whole card must be one
        // button, with the state details folded into one description.
        composeRule.setContent {
            MediaCard(
                title = "Filme único",
                imageUrl = null,
                qualityBadge = "4K",
            )
        }

        val node = composeRule
            .onNodeWithContentDescription("Abrir Filme único", substring = true)
            .fetchSemanticsNode()

        assertEquals(
            "the card must expose exactly one merged description",
            1,
            node.config.getOrNull(SemanticsProperties.ContentDescription).orEmpty().size,
        )
        assertTrue(
            "the card must expose a single click action",
            node.config.contains(SemanticsActions.OnClick),
        )
    }

    /**
     * The broken-artwork fallback draws the title inside the image area, and the
     * card draws it again underneath. Both labels reach the merged node's `Text`
     * property, so an accessibility service read the title twice — measured on
     * the device as `Text = '[Filme único, 4K, Filme único]'`.
     *
     * `invisibleToUser()` on the fallback was tried and measured first: it did
     * **not** remove the text from the merged property, because it only hides a
     * node from services. Clearing the fallback's semantics does remove it, and
     * the visible drawing is unchanged — the fallback is decoration, and the
     * card's own `contentDescription` already carries the title.
     */
    @Test
    fun mediaCardAnnouncesItsTitleOnceWhenArtworkFailsToLoad() {
        composeRule.setContent {
            MediaCard(
                title = "Filme único",
                imageUrl = null,
                qualityBadge = "4K",
            )
        }

        val node = composeRule
            .onNodeWithContentDescription("Abrir Filme único", substring = true)
            .fetchSemanticsNode()

        val mergedText = node.config
            .getOrNull(SemanticsProperties.Text)
            .orEmpty()
            .map { it.text }

        assertEquals(
            "the fallback must not repeat the title in the accessibility tree",
            1,
            mergedText.count { it == "Filme único" },
        )
        assertEquals(
            "the card's own label must still carry the title",
            1,
            node.config
                .getOrNull(SemanticsProperties.ContentDescription)
                .orEmpty()
                .single()
                .windowed("Filme único".length)
                .count { it == "Filme único" },
        )
    }

    @Test
    fun mediaCardStillExposesItsFullAccessibilityLabel() {
        composeRule.setContent {
            MediaCard(
                title = "Filme único",
                imageUrl = null,
                isWatched = true,
                isFavorite = true,
                qualityBadge = "4K",
                progress = 0.4f,
            )
        }

        val descriptions = composeRule
            .onNodeWithContentDescription("Abrir Filme único", substring = true)
            .fetchSemanticsNode()
            .config
            .getOrNull(SemanticsProperties.ContentDescription)
            .orEmpty()

        assertEquals(
            listOf(
                mediaCardAccessibilityLabel(
                    title = "Filme único",
                    isLive = false,
                    isWatched = true,
                    isFavorite = true,
                    qualityBadge = "4K",
                    unplayedCount = 0,
                    progress = 0.4f,
                ),
            ),
            descriptions,
        )
    }

    @Test
    fun mediaCardDoesNotAnnounceDecorativeBadgeText() {
        composeRule.setContent {
            MediaCard(
                title = "Canal",
                imageUrl = null,
                isLive = true,
            )
        }

        val label = composeRule
            .onNodeWithContentDescription("Abrir Canal", substring = true)
            .fetchSemanticsNode()
            .config
            .getOrNull(SemanticsProperties.ContentDescription)
            .orEmpty()
            .single()

        assertTrue("the live state must be announced", label.contains("ao vivo"))
        assertFalse(
            "the badge text is decorative and must not be concatenated in its raw form",
            label.contains("AO VIVO"),
        )
    }
}