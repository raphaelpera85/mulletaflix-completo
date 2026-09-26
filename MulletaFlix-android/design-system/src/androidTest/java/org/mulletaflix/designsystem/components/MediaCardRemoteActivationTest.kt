package org.mulletaflix.designsystem.components

import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assume
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * Reported by the user: "usando na tv tenho que clicar 2x para entrar em qualquer
 * biblioteca ou midia".
 *
 * A `MediaCard` on a remote surface had **two focus targets on one node**:
 * `Modifier.focusable()` (added only when `focusFriendly`) plus the focus target
 * `Modifier.clickable` creates for itself. The remote's centre key went to the
 * target that did not hold the activation handler, so the first press was spent
 * and only the second opened the card.
 *
 * This test presses the remote's centre key exactly once on a card that already
 * holds focus, which is the user's situation after navigating onto it.
 */
@RunWith(AndroidJUnit4::class)
class MediaCardRemoteActivationTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun showCard(clicks: () -> Unit) {
        composeRule.setContent {
            MulletaFlixTheme {
                MediaCard(
                    title = "Filme",
                    imageUrl = null,
                    focusFriendly = true,
                    onClick = clicks,
                )
            }
        }
    }

    @Test
    fun aFocusedRemoteCardOpensOnTheFirstCentrePress() {
        assumeTelevisionSurface()
        var clicks = 0
        showCard { clicks++ }

        val card = composeRule.onNodeWithContentDescription("Abrir Filme")
        card.requestFocus()
        card.assertIsFocused()

        card.performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.waitForIdle()

        assertEquals(
            "a card must open with one press of the remote; if this is 0 the first " +
                "press was swallowed by a second focus target",
            1,
            clicks,
        )
    }

    @Test
    fun aSecondCentrePressDoesNotOpenACardTwice() {
        assumeTelevisionSurface()
        var clicks = 0
        showCard { clicks++ }

        val card = composeRule.onNodeWithContentDescription("Abrir Filme")
        card.requestFocus()

        card.performKeyInput { pressKey(Key.DirectionCenter) }
        card.performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.waitForIdle()

        assertEquals("two presses must mean two activations, not three", 2, clicks)
    }
}
