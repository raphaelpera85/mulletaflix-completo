package org.mulletaflix.designsystem.components

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * Reported by the user on a phone: "os icones do menu superior estao muito grandes no
 * celular". They were right, and this is the measurement.
 *
 * The action was a `Box` with a fixed 48 dp size and `propagateMinConstraints = true`
 * holding the caller's icon directly, which handed the *touch target's* size to the
 * artwork. Measured on the phone emulator (density 420, sizes in pixels):
 *
 * | Shape | Node | Drawn glyph |
 * |---|---|---|
 * | shipped in v1.2.56–v1.2.59 | 126 px (48 dp) | **84 px (32 dp)** |
 * | fixed | 126 px (48 dp) | **43 px (24 dp)** |
 * | bare Material `Icon` | — | 43 px (24 dp) |
 *
 * The reachable regression reproduced here is the same rule the released code broke:
 * an icon that fills the node gets drawn at the *node's* size, not at the icon size.
 * That is exactly what the shipped shape did, so it is what the test renders.
 *
 * The expectation is anchored to a bare 24 dp `Icon` measured in the same
 * composition, so it is not a magic number.
 */
@RunWith(AndroidJUnit4::class)
class TopBarActionIconSizeTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun theGlyphIsNotStretchedToTheInteractiveTarget() {
        // Measured through the semantics tree rather than a pixel capture: the node's
        // own bounds are exactly what "the artwork filled its target" changes. The
        // released shape reported a 48 dp glyph here, because the wrapping `Box` handed
        // its own min constraints down; a bare Material icon reports 24 dp.
        composeRule.setContent {
            MulletaFlixTheme {
                Row {
                    MulletaFlixTopBarAction(
                        onClick = {},
                        focusFriendly = false,
                        modifier = Modifier.testTag(TOP_BAR_ACTION_TEST_TAG),
                    ) {
                        Icon(
                            Icons.Default.Refresh,
                            contentDescription = "Atualizar",
                            modifier = Modifier.testTag("glyph"),
                        )
                    }
                    Icon(
                        imageVector = Icons.Default.Refresh,
                        contentDescription = "Referência 24 dp",
                        modifier = Modifier.size(24.dp).testTag("reference"),
                    )
                }
            }
        }
        composeRule.waitForIdle()

        val glyph = composeRule.onNodeWithTag("glyph", useUnmergedTree = true).fetchSemanticsNode().size
        val reference = composeRule.onNodeWithTag("reference", useUnmergedTree = true).fetchSemanticsNode().size

        assertTrue("the action must lay its icon out at all", glyph.height > 0)
        assertEquals(
            "the icon inside a top-bar action must keep the Material icon height, not " +
                "the interactive target's: measured ${glyph.width}x${glyph.height} against " +
                "${reference.width}x${reference.height} for a bare 24 dp icon",
            reference.height,
            glyph.height,
        )
    }

    @Test
    fun theActionIsTheSameSizeAsAPlainIconButton() {
        // The second half of the report: the action must not be a bigger control than
        // the platform's own. The released shape pinned the wrapping box to 48 dp, and
        // Material's `IconButton` already carries that minimum, so the two disagreed.
        composeRule.setContent {
            MulletaFlixTheme {
                Row {
                    MulletaFlixTopBarAction(
                        onClick = {},
                        focusFriendly = false,
                        modifier = Modifier.testTag(TOP_BAR_ACTION_TEST_TAG),
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Atualizar")
                    }
                    IconButton(onClick = {}, modifier = Modifier.testTag("reference")) {
                        Icon(Icons.Default.Refresh, contentDescription = "Referência")
                    }
                }
            }
        }
        composeRule.waitForIdle()

        val action = composeRule.onNodeWithTag(TOP_BAR_ACTION_TEST_TAG).fetchSemanticsNode().size
        val reference = composeRule.onNodeWithTag("reference", useUnmergedTree = true).fetchSemanticsNode().size

        assertEquals(
            "a top-bar action must occupy the same box as the platform's own IconButton: " +
                "measured ${action.width}x${action.height} against " +
                "${reference.width}x${reference.height}",
            reference.height,
            action.height,
        )
        assertEquals(
            "and the same width: measured ${action.width} against ${reference.width}",
            reference.width,
            action.width,
        )
    }

    @Test
    fun aGlyphThatFillsItsNodeIsWhatTheUserSaw() {
        // Reproduces the shipped shape instead of mutating production code: a node with
        // a fixed 48 dp size and `propagateMinConstraints = true` lets the icon fill it.
        composeRule.setContent {
            MulletaFlixTheme {
                Row {
                    Box(
                        contentAlignment = Alignment.Center,
                        modifier = Modifier
                            .size(TOP_BAR_ACTION_SIZE_DP.dp)
                            .testTag("filled"),
                        propagateMinConstraints = true,
                    ) {
                        Icon(Icons.Default.Refresh, contentDescription = "Preenchido")
                    }
                    Icon(
                        imageVector = Icons.Default.Refresh,
                        contentDescription = "Referência 24 dp",
                        modifier = Modifier.size(24.dp).testTag("reference"),
                    )
                }
            }
        }
        composeRule.waitForIdle()

        val filled = composeRule
            .onNodeWithTag("filled", useUnmergedTree = true)
            .fetchSemanticsNode()
            .size
        val reference = composeRule
            .onNodeWithTag("reference", useUnmergedTree = true)
            .fetchSemanticsNode()
            .size

        assertTrue(
            "this shape is the defect: the icon filled its node, measuring " +
                "${filled.width}x${filled.height} against ${reference.width}x${reference.height} " +
                "for a 24 dp icon. If this stops holding, the reason the top bar looked " +
                "oversized on a phone has changed and the guard above needs re-deriving.",
            filled.height > reference.height,
        )
    }
}
