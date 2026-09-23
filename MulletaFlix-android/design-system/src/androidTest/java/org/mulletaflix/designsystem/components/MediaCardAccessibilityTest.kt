package org.mulletaflix.designsystem.components

import androidx.compose.foundation.layout.width
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertHasClickAction
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.getOrNull
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.runtime.remember
import androidx.compose.ui.unit.dp
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
    fun nonClickableMediaCard_isNotAnnouncedAsAButton() {
        // O card não clicável publicava `role = Role.Button` sem ação nenhuma e com o
        // mesmo rótulo da linha clicável que o contém: o leitor de tela encontrava o
        // item duas vezes, e uma delas era um botão que não fazia nada. A expectativa
        // anterior deste teste prendia justamente esse nó ("Abrir Linha de
        // biblioteca" existindo, só sem ação de clique).
        composeRule.setContent {
            MediaCard(
                title = "Linha de biblioteca",
                imageUrl = null,
                isClickable = false,
            )
        }

        assertTrue(
            "um card sem ação não pode anunciar o item; quem anuncia é a linha",
            composeRule
                .onAllNodes(hasContentDescription("Abrir Linha de biblioteca", substring = true))
                .fetchSemanticsNodes()
                .isEmpty(),
        )
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

    /**
     * Uma capa que falha não pode mudar o tamanho do item.
     *
     * O aviso de acessibilidade do estado de falha já tinha teste; faltava a outra metade
     * da promessa: "sem quebrar o scroll". O card dimensiona a arte por `aspectRatio`, não
     * pela imagem — se dependesse da imagem, uma capa que não carrega deixaria o item com
     * a altura do que sobrar e a fileira inteira pularia no meio da rolagem, empurrando o
     * que o espectador estava prestes a tocar.
     *
     * **A asserção é uma faixa, e não um mínimo.** A primeira versão pedia `>= 180 dp` e
     * não discriminava nada: sem o `aspectRatio`, o `fillMaxSize` interno estica a arte
     * para a altura da tela e a medida sobe para 540 dp — que também passa de 180.
     * Retrato é 2:3, então 120 dp de largura dão exatamente 180 dp de arte; o resto são o
     * título (no máximo duas linhas) e o espaçamento. Medido: 540 dp sem o aspecto, e
     * dentro da faixa com ele.
     */
    @Test
    fun aFailedArtworkKeepsTheCardAtItsFullSize() {
        composeRule.setContent {
            MediaCard(
                title = "Filme sem capa",
                imageUrl = null,
                shape = MediaCardShape.Portrait,
                modifier = Modifier.width(120.dp),
            )
        }

        val node = composeRule
            .onNodeWithContentDescription("Abrir Filme sem capa", substring = true)
            .fetchSemanticsNode()
        val heightDp = with(composeRule.density) { node.boundsInRoot.height.toDp().value }

        assertTrue(
            "o card precisa ter a altura que o aspecto manda (180 dp de arte + título, " +
                "até 260 dp); medido: ${heightDp}dp",
            heightDp in 180f..260f,
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