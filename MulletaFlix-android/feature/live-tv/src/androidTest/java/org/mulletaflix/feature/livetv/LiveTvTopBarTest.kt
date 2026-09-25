package org.mulletaflix.feature.livetv

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.assertIsNotFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/** Regression coverage for the Live TV actions used with an Android TV remote. */
@RunWith(AndroidJUnit4::class)
class LiveTvTopBarTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun remoteMovesFromRefreshToGuideAndBothActionsInvokeCallbacks() {
        var refreshCalls = 0
        var guideCalls = 0
        composeRule.setContent {
            MulletaFlixTheme {
                Box(Modifier.fillMaxSize().background(Color.Black)) {
                    LiveTvTopBar(
                        onBack = {},
                        onRefresh = { refreshCalls++ },
                        onGuide = { guideCalls++ },
                        isLoading = false,
                        isLoadingGuide = false,
                        hasChannels = true,
                    )
                }
            }
        }

        val refresh = composeRule.onNodeWithContentDescription("Atualizar canais")
        val guide = composeRule.onNodeWithContentDescription("Guia EPG")
        refresh.requestFocus()
        refresh.assertIsFocused()
        refresh.performClick()
        refresh.performKeyInput { pressKey(Key.DirectionRight) }
        composeRule.waitForIdle()
        guide.assertIsFocused()
        refresh.assertIsNotFocused()
        guide.performClick()

        composeRule.runOnIdle {
            assertEquals(1, refreshCalls)
            assertEquals(1, guideCalls)
        }
    }

    @Test
    fun guideIsDisabledUntilChannelsExist() {
        composeRule.setContent {
            MulletaFlixTheme {
                LiveTvTopBar(
                    onBack = {},
                    onRefresh = {},
                    onGuide = {},
                    isLoading = false,
                    isLoadingGuide = false,
                    hasChannels = false,
                )
            }
        }

        val guide = composeRule.onNodeWithContentDescription("Guia EPG")
        guide.assertIsNotEnabled()
    }

    @Test
    fun loadingStatesRemainAccessible() {
        composeRule.setContent {
            MulletaFlixTheme {
                LiveTvTopBar(
                    onBack = {},
                    onRefresh = {},
                    onGuide = {},
                    isLoading = true,
                    isLoadingGuide = true,
                    hasChannels = true,
                )
            }
        }

        composeRule.onNodeWithContentDescription("Atualizar canais").assertIsEnabled()
        composeRule.onNodeWithContentDescription("Guia EPG").assertIsEnabled()
    }
}
