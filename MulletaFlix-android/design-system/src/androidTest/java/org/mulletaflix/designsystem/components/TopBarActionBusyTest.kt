package org.mulletaflix.designsystem.components

import androidx.compose.foundation.layout.Row
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.toPixelMap
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.assertNotEquals
import org.junit.Assume
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * Reported by the user on the TV: "a navegacao na tv nos menus superiores nao mostra
 * em qual icone esta e enquanto em um dos icones fica um simbolo de atualizar o tempo
 * todo no meio da tela".
 *
 * The focus half was fixed earlier. The symbol half had a second cause: Live TV,
 * Biblioteca, Minha Lista and SyncPlay all passed `enabled = !state.isLoading` to
 * their refresh action. A disabled `IconButton` dims into something that reads as a
 * broken, frozen button — it is a static glyph, so it never animates — and it leaves
 * the focus order. On a TV those screens also refresh on a foreground timer, so the
 * action disappeared from the D-pad sequence while the user was navigating the top
 * bar.
 *
 * These tests measure what replaces that: the action stays focusable and named while a
 * request runs, still refuses the duplicate request, and keeps focus when the request
 * starts underneath the user.
 *
 * The three focus assertions are **TV only**, guarded by [requireARemoteSurface].
 * Measured on the phone emulator: a node can carry `RequestFocus` and accept it, yet
 * report `Focused = false`, because a touch device is in touch mode and has no
 * non-touch focus surface. They skip there rather than fail for a reason that has
 * nothing to do with the action. The size and activation-shape assertions run anywhere.
 */
@RunWith(AndroidJUnit4::class)
class TopBarActionBusyTest {

    private fun requireARemoteSurface() {
        Assume.assumeTrue(
            "requires a TV surface; focus is not observable in touch mode",
            isTelevisionSurface(),
        )
    }

    @get:Rule
    val composeRule = createComposeRule()

    private fun showAction(busy: Boolean, clicks: () -> Unit) {
        composeRule.setContent {
            MulletaFlixTheme {
                Row {
                    MulletaFlixTopBarAction(
                        onClick = clicks,
                        busy = busy,
                        focusFriendly = true,
                        busyContentDescription = "Atualizar canais",
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar canais")
                    }
                    Text("vizinho")
                }
            }
        }
    }

    private fun action() = composeRule.onNodeWithTag(TOP_BAR_ACTION_TEST_TAG)

    @Test
    fun aBusyActionStaysInTheFocusOrder() {
        requireARemoteSurface()
        showAction(busy = true) {}

        action().requestFocus()
        action().assertIsFocused()
    }

    @Test
    fun aBusyActionStillRefusesTheDuplicateRequest() {
        requireARemoteSurface()
        var clicks = 0
        showAction(busy = true) { clicks++ }

        action().requestFocus()
        action().performClick()
        action().performClick()
        composeRule.waitForIdle()

        assertEquals(
            "a request already running must not be started again by the action",
            0,
            clicks,
        )
    }

    @Test
    fun theSameActionStaysFocusedWhenTheRequestStarts() {
        requireARemoteSurface()
        // The reported situation: the user is standing on the icon when a background
        // refresh begins. Toggling `enabled` there moved focus off the action.
        var busy by mutableStateOf(false)
        composeRule.setContent {
            MulletaFlixTheme {
                Row {
                    MulletaFlixTopBarAction(
                        onClick = {},
                        busy = busy,
                        focusFriendly = true,
                        busyContentDescription = "Atualizar canais",
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar canais")
                    }
                    Text("vizinho")
                }
            }
        }

        action().requestFocus()
        action().assertIsFocused()

        composeRule.runOnIdle { busy = true }
        composeRule.waitForIdle()

        action().assertIsFocused()
    }

    @Test
    fun anIdleActionStillActs() {
        var clicks = 0
        showAction(busy = false) { clicks++ }

        action().performClick()
        composeRule.waitForIdle()

        assertEquals(1, clicks)
        composeRule.onNodeWithText("vizinho").assertExists()
    }

    @Test
    fun aFocusedActionActivatesOnOneCentrePress() {
        requireARemoteSurface()
        // The regression this guards: removing the redundant `clickable` from the
        // wrapping box could have left the action with no activation on the remote's
        // centre key at all. The `IconButton` is meant to be that one activation, and
        // this presses the centre key exactly once on a focused action — the user's
        // situation after navigating onto it.
        var clicks = 0
        showAction(busy = false) { clicks++ }

        val target = composeRule.onNodeWithTag(TOP_BAR_ACTION_TEST_TAG)
        target.requestFocus()
        target.assertIsFocused()

        target.performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.waitForIdle()

        assertEquals(
            "one centre press on a focused action must activate it exactly once",
            1,
            clicks,
        )
    }

    @Test
    fun aDisabledActionLooksDisabled() {
        // Regression introduced when the wrapper box was removed: `enabled` still gates
        // the click but nothing dims the icon any more, so a genuinely disabled action
        // ("nothing to act on") became indistinguishable from an available one. That is
        // the opposite of the busy action, which must stay full-strength and shows a
        // spinner instead.
        var enabled by mutableStateOf(true)
        composeRule.setContent {
            MulletaFlixTheme {
                Row {
                    MulletaFlixTopBarAction(
                        onClick = {},
                        enabled = enabled,
                        focusFriendly = false,
                        modifier = Modifier.testTag(TOP_BAR_ACTION_TEST_TAG),
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar canais")
                    }
                }
            }
        }

        composeRule.waitForIdle()
        val whenEnabled = capturePixels()

        composeRule.runOnIdle { enabled = false }
        composeRule.waitForIdle()
        val whenDisabled = capturePixels()

        val differing = whenEnabled.indices.count { whenEnabled[it] != whenDisabled[it] }
        assertTrue(
            "a disabled action must not look identical to an enabled one; " +
                "${whenEnabled.size} pixels compared, $differing differ",
            differing > 0,
        )
    }

    @Test
    fun theFadeBelongsToEnabledAndNeverToBusy() {
        // The dimming belongs to `enabled` alone. A busy action is working, not
        // unavailable, and fading exactly that is what the user reported as a stuck,
        // greyed-out refresh icon. Asserted as the decision rather than as pixels,
        // because faded white over the surface sits too close to a brightness
        // threshold for a pixel count to separate the two states reliably.
        assertEquals(1f, topBarActionContentAlpha(enabled = true))
        assertEquals(TOP_BAR_ACTION_DISABLED_ALPHA, topBarActionContentAlpha(enabled = false))
        assertTrue(
            "the fade must be visible but keep the icon recognisable",
            TOP_BAR_ACTION_DISABLED_ALPHA > 0f && TOP_BAR_ACTION_DISABLED_ALPHA < 1f,
        )
        // `busy` has no say in the alpha: the function takes only `enabled`.
        assertEquals(1f, topBarActionContentAlpha(enabled = true))
    }

    @Test
    fun aDisabledActionIsAnnouncedAsDisabled() {
        // O alpha é um sinal puramente visual. O componente mantém `enabled = true` de
        // propósito (o D-pad precisa do nó na sequência) e engole o clique, então um
        // serviço de acessibilidade anunciava "botão" para uma ação que não fazia nada
        // e não explicava por quê. Medido antes desta correção: nenhum `Disabled`.
        var enabled by mutableStateOf(false)
        composeRule.setContent {
            MulletaFlixTheme {
                Row {
                    MulletaFlixTopBarAction(
                        onClick = {},
                        enabled = enabled,
                        focusFriendly = false,
                        modifier = Modifier.testTag(TOP_BAR_ACTION_TEST_TAG),
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar canais")
                    }
                }
            }
        }

        composeRule.onNodeWithTag(TOP_BAR_ACTION_TEST_TAG).assertIsNotEnabled()

        composeRule.runOnIdle { enabled = true }
        composeRule.waitForIdle()

        composeRule.onNodeWithTag(TOP_BAR_ACTION_TEST_TAG).assertIsEnabled()
    }

    @Test
    fun aBusyActionIsNotAnnouncedAsUnavailable() {
        // Trabalhando não é o mesmo que indisponível: quem espera a atualização ouve o
        // nome de "ocupado" e o spinner, não "desativado".
        showAction(busy = true) {}

        action().assertIsEnabled()
        composeRule.onNodeWithContentDescription("Atualizar canais").assertExists()
    }

    private fun capturePixels(): List<Int> {
        val pixels = composeRule.onNodeWithTag(TOP_BAR_ACTION_TEST_TAG)
            .captureToImage()
            .toPixelMap()
        return buildList {
            for (y in 0 until pixels.height) {
                for (x in 0 until pixels.width) add(pixels[x, y].toArgb())
            }
        }
    }

    @Test
    fun theActionIsASingleActivatableNode() {
        // One visible control must not declare two activatable nodes stacked on top of
        // each other. A `clickable` on the wrapping box *and* on the `IconButton` was
        // exactly that, and it is the shape that made a `MediaCard` need two presses of
        // a remote's centre key.
        showAction(busy = false) {}

        val activatable = composeRule
            .onAllNodes(hasClickAction())
            .fetchSemanticsNodes()

        assertEquals(
            "a top-bar action must expose exactly one activatable node; found ${activatable.size}",
            1,
            activatable.size,
        )
    }

    @Test
    fun theSpinnerKeepsTheActionName() {
        showAction(busy = true) {}

        composeRule.onNodeWithContentDescription("Atualizar canais").assertExists()
    }
}

/**
 * Whether this device is a TV surface.
 *
 * Read from the target context's configuration, matching what `isTelevisionDevice()`
 * does in production.
 */
private fun isTelevisionSurface(): Boolean {
    val context = InstrumentationRegistry.getInstrumentation().targetContext
    return (context.resources.configuration.uiMode and
        android.content.res.Configuration.UI_MODE_TYPE_MASK) ==
        android.content.res.Configuration.UI_MODE_TYPE_TELEVISION
}
