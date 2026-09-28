package org.mulletaflix.feature.library

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.grid.rememberLazyGridState
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import org.mulletaflix.designsystem.components.MediaCard
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class LibraryGridDensityRenderTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun narrowTelevisionGridRendersFourCardsAtUsableWidth() {
        assumeTelevisionProfile()
        var densityScale = 1f
        val viewportWidthDp = 320
        val columns = libraryGridColumns(
            widthDp = viewportWidthDp,
            density = LIBRARY_GRID_DENSITY_COMFORTABLE,
            isTelevision = true,
        )

        composeRule.setContent {
            val currentDensityScale = LocalDensity.current.density
            SideEffect { densityScale = currentDensityScale }
            MulletaFlixTheme {
                Box(modifier = Modifier.width(viewportWidthDp.dp).height(180.dp)) {
                    LibraryGridLayout(
                        state = rememberLazyGridState(),
                        viewportWidthDp = viewportWidthDp,
                        density = LIBRARY_GRID_DENSITY_COMFORTABLE,
                        isTelevision = true,
                        isTablet = false,
                        isGridView = true,
                    ) {
                        items((0..(columns * 2)).toList(), key = { it }) { index ->
                            MediaCard(
                                title = "Título $index",
                                imageUrl = null,
                                modifier = Modifier.fillMaxWidth().testTag("library-grid-card-$index"),
                                shape = MediaCardShape.Portrait,
                                focusFriendly = true,
                                onClick = {},
                            )
                        }
                    }
                }
            }
        }

        composeRule.waitForIdle()
        val renderedBounds = (0..columns).map { index ->
            composeRule.onNodeWithTag("library-grid-card-$index").fetchSemanticsNode().boundsInRoot
        }
        val renderedWidthsDp = renderedBounds.take(columns).map { it.width / densityScale }
        val firstRowTop = renderedBounds.first().top
        val nextRowTop = renderedBounds[columns].top

        assertEquals(4, columns)
        assertTrue(
            "The first row did not place all cards side by side: $renderedBounds",
            renderedBounds.take(columns).all { kotlin.math.abs(it.top - firstRowTop) < densityScale },
        )
        assertTrue("The next card was not laid out on a lower row", nextRowTop > firstRowTop + densityScale)
        assertTrue(
            "Cards did not advance horizontally in the first row: $renderedBounds",
            renderedBounds.take(columns).zipWithNext().all { (left, right) -> right.left > left.left },
        )
        assertTrue(
            "Rendered card widths $renderedWidthsDp dp fall below the configured TV minimum",
            renderedWidthsDp.all { it + 0.5f >= libraryGridMinimumCardSizeDp(viewportWidthDp, LIBRARY_GRID_DENSITY_COMFORTABLE) },
        )

        val firstCard = composeRule.onNodeWithTag("library-grid-card-0")
        firstCard.requestFocus().assertIsFocused()
        firstCard.performKeyInput { pressKey(Key.DirectionRight) }
        val secondCard = composeRule.onNodeWithTag("library-grid-card-1")
        secondCard.assertIsFocused()
        secondCard.performKeyInput { pressKey(Key.DirectionDown) }
        composeRule.onNodeWithTag("library-grid-card-5").assertIsFocused()
    }
}
