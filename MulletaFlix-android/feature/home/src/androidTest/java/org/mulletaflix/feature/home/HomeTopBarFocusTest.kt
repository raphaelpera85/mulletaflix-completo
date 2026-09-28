package org.mulletaflix.feature.home

import android.os.Build
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PixelMap
import androidx.compose.ui.graphics.toPixelMap
import androidx.compose.ui.unit.dp
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsNotFocused
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.filters.SdkSuppress
import org.junit.Assert.assertTrue
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.atomic.AtomicInteger
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * The Home top bar on TV: a remote must visibly land on one icon at a time.
 *
 * Reported by the user: "a navegação na tv nos menus superiores não mostra em
 * qual icone está". `IconButton` is focusable, so the D-pad already moved across
 * the Home actions — but every icon kept its fixed `tint` and nothing was drawn
 * around it, so the focus was invisible.
 *
 * These tests measure pixels rather than trusting the code: the focused icon is
 * captured and compared to the same icon when it does not hold focus. A
 * translucent focus state layer (the Material ripple) is grey, so the red bias
 * asserted here can only come from the ring and disc this fix adds.
 */
@RunWith(AndroidJUnit4::class)
@SdkSuppress(minSdkVersion = Build.VERSION_CODES.O)
class HomeTopBarFocusTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun showTopBar(focusFriendly: Boolean, onRequestMedia: () -> Unit = {}) {
        val spec = if (focusFriendly) {
            homeLayoutSpec(HomeDeviceClass.TV)
        } else {
            homeLayoutSpec(HomeDeviceClass.PHONE)
        }
        composeRule.setContent {
            MulletaFlixTheme {
                Box(Modifier.fillMaxSize().background(Color.Black)) {
                    HomeTopBar(
                        profile = null,
                        layoutSpec = spec,
                        isTelevision = focusFriendly,
                        onSearch = {},
                        onLiveTv = {},
                        onDownloads = {},
                        onFavorites = {},
                        onSettings = {},
                        onProfile = {},
                        onRefresh = {},
                        onRequestMedia = onRequestMedia,
                        isRefreshing = false,
                    )
                }
            }
        }
    }

    @Test
    fun requestMediaActionHasAccessibleNameAndOpensRequestFlow() {
        var requestCount = 0
        showTopBar(focusFriendly = false, onRequestMedia = { requestCount++ })

        composeRule.onNodeWithContentDescription("Solicitar mídia").performClick()

        assertEquals(1, requestCount)
    }

    @Test
    fun phoneTopBarKeepsPrimaryActionsVisibleAndMovesOtherActionsIntoMenu() {
        showTopBar(focusFriendly = false)

        composeRule.onNodeWithContentDescription("Solicitar mídia").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Buscar").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Minha Lista").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Mais ações").assertIsDisplayed()
        val contentRight = composeRule.onRoot().fetchSemanticsNode().boundsInRoot.right
        listOf("Solicitar mídia", "Buscar", "Minha Lista", "Mais ações").forEach { description ->
            val actionRight = composeRule.onNodeWithContentDescription(description)
                .fetchSemanticsNode().boundsInRoot.right
            assertTrue("$description deve caber na largura da tela", actionRight <= contentRight + 1f)
        }
        composeRule.onNodeWithContentDescription("Mais ações").performClick()
        composeRule.onNodeWithText("Configurações").assertIsDisplayed()
        composeRule.onNodeWithText("TV Ao Vivo").assertIsDisplayed()
        composeRule.onNodeWithText("Downloads").assertIsDisplayed()
    }

    @Test
    fun tvTopBarHidesDownloadsEntry() {
        showTopBar(focusFriendly = true)

        composeRule.onNodeWithContentDescription("Downloads").assertDoesNotExist()
        composeRule.onNodeWithText("Downloads").assertDoesNotExist()
    }

    @Test
    fun phoneTopBarKeepsOverflowMenuVisibleOnNarrowScreens() {
        composeRule.setContent {
            MulletaFlixTheme {
                Box(Modifier.width(320.dp).height(96.dp).background(Color.Black)) {
                    HomeTopBar(
                        profile = null,
                        layoutSpec = homeLayoutSpec(HomeDeviceClass.PHONE),
                        onSearch = {},
                        onLiveTv = {},
                        onDownloads = {},
                        onFavorites = {},
                        onSettings = {},
                        onProfile = {},
                        onRefresh = {},
                        onRequestMedia = {},
                        isRefreshing = false,
                    )
                }
            }
        }

        composeRule.onNodeWithContentDescription("Solicitar mídia").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Buscar").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Minha Lista").assertIsDisplayed()
        composeRule.onNodeWithContentDescription("Mais ações").assertIsDisplayed()
        val contentRight = composeRule.onRoot().fetchSemanticsNode().boundsInRoot.right
        listOf("Solicitar mídia", "Buscar", "Minha Lista", "Mais ações").forEach { description ->
            val actionRight = composeRule.onNodeWithContentDescription(description)
                .fetchSemanticsNode().boundsInRoot.right
            assertTrue("$description deve caber na largura da tela", actionRight <= contentRight + 1f)
        }
        composeRule.onNodeWithContentDescription("Mais ações").performClick()
        composeRule.onNodeWithText("Configurações").assertIsDisplayed()
    }

    /** Average `red - green` over the node, which is ~0 for grey and high for the red ring. */
    private fun redGreenBias(pixels: PixelMap): Int {
        var total = 0L
        var samples = 0
        for (y in 0 until pixels.height) {
            for (x in 0 until pixels.width) {
                val color = pixels[x, y]
                total += (color.red * 255f).toInt() - (color.green * 255f).toInt()
                samples++
            }
        }
        return if (samples == 0) 0 else (total / samples).toInt()
    }

    private fun biasOf(description: String): Int =
        redGreenBias(
            composeRule.onNodeWithContentDescription(description).captureToImage().toPixelMap(),
        )

    @Test
    fun remoteNavigationMovesFocusAcrossTheTopBarIcons() {
        assumeTelevisionProfile()
        showTopBar(focusFriendly = true)

        composeRule.onNodeWithContentDescription("Buscar").requestFocus()
        composeRule.onNodeWithContentDescription("Buscar").assertIsFocused()

        composeRule.onNodeWithContentDescription("Buscar").performKeyInput {
            pressKey(Key.DirectionRight)
        }
        composeRule.waitForIdle()

        composeRule.onNodeWithContentDescription("TV Ao Vivo").assertIsFocused()
        composeRule.onNodeWithContentDescription("Buscar").assertIsNotFocused()
    }

    @Test
    fun theFocusedIconIsPaintedDifferentlyFromAnUnfocusedOne() {
        assumeTelevisionProfile()
        showTopBar(focusFriendly = true)

        // Reference: the remote is on the neighbour, so "Buscar" is inert.
        composeRule.onNodeWithContentDescription("TV Ao Vivo").requestFocus()
        composeRule.waitForIdle()
        val unfocusedBias = biasOf("Buscar")

        composeRule.onNodeWithContentDescription("Buscar").requestFocus()
        composeRule.waitForIdle()
        val focusedBias = biasOf("Buscar")

        assertTrue(
            "an unfocused action must stay neutral; measured red bias was $unfocusedBias",
            unfocusedBias <= 5,
        )
        assertTrue(
            "the focused action must be visibly marked; measured red bias was $focusedBias",
            focusedBias >= 20,
        )
    }

    @Test
    fun touchLayoutsDoNotDrawAFocusRing() {
        // A phone taps these icons and never focuses them; painting them red
        // would be permanent noise instead of feedback.
        showTopBar(focusFriendly = false)

        composeRule.onNodeWithContentDescription("Minha Lista").requestFocus()
        composeRule.waitForIdle()
        val unfocusedBias = biasOf("Buscar")

        composeRule.onNodeWithContentDescription("Buscar").requestFocus()
        composeRule.waitForIdle()
        val focusedBias = biasOf("Buscar")

        assertTrue(
            "a phone must not paint a focus ring; measured red bias was $focusedBias",
            focusedBias <= 5,
        )
        assertTrue(
            "the unfocused baseline must also be neutral; measured $unfocusedBias",
            unfocusedBias <= 5,
        )
    }

    @Test
    fun refreshActionIsAvailableToTheRemoteAndInvokesHomeRefresh() {
        assumeTelevisionProfile()
        val refreshCalls = AtomicInteger(0)
        val spec = homeLayoutSpec(HomeDeviceClass.TV)
        composeRule.setContent {
            MulletaFlixTheme {
                Box(Modifier.fillMaxSize().background(Color.Black)) {
                    HomeTopBar(
                        profile = null,
                        layoutSpec = spec,
                        onSearch = {},
                        onLiveTv = {},
                        onDownloads = {},
                        onFavorites = {},
                        onSettings = {},
                        onProfile = {},
                        onRefresh = { refreshCalls.incrementAndGet() },
                        isRefreshing = false,
                    )
                }
            }
        }

        composeRule.onNodeWithContentDescription("Atualizar Home").performClick()
        assertEquals(1, refreshCalls.get())
    }
}
