package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_80
import org.mulletaflix.domain.model.SUBTITLE_COLOR_CYAN
import android.content.res.Configuration
import androidx.test.platform.app.InstrumentationRegistry

@RunWith(AndroidJUnit4::class)
class PlayerSubtitleAppearanceMenuTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun selectionReportsSizeColorAndBackgroundCodes() {
        val selections = mutableListOf<String>()
        composeRule.setContent {
            MaterialTheme {
                PlayerSubtitleAppearanceMenu(
                    state = PlayerState(subtitleFontSize = 100),
                    onSubtitleFontSizeSelect = { selections += "size:$it" },
                    onSubtitleColorSelect = { selections += "color:$it" },
                    onSubtitleBackgroundSelect = { selections += "background:$it" },
                    onDismiss = {},
                )
            }
        }

        val isCompactWidth = InstrumentationRegistry.getInstrumentation().targetContext
            .resources.configuration.screenWidthDp < 600
        composeRule.onNodeWithText("200%").performClick()
        val cyan = composeRule.onNodeWithText("Ciano")
        if (isCompactWidth) cyan.performScrollTo()
        cyan.performClick()
        val darkBackground = composeRule.onNodeWithText("Preto 80%")
        if (isCompactWidth) darkBackground.performScrollTo()
        darkBackground.performClick()

        assertEquals(
            listOf("size:200", "color:$SUBTITLE_COLOR_CYAN", "background:$SUBTITLE_BACKGROUND_BLACK_80"),
            selections,
        )
    }

    @Test
    fun subtitleTrackMenuOpensTheAppearanceControls() {
        var appearanceOpened = false
        composeRule.setContent {
            MaterialTheme {
                PlayerTrackMenu(
                    title = "Legendas",
                    tracks = emptyList(),
                    selectedIndex = -1,
                    onSelect = {},
                    onDismiss = {},
                    onAppearanceClick = { appearanceOpened = true },
                )
            }
        }

        composeRule.onNodeWithText("Personalizar aparência").performClick()
        assertTrue(appearanceOpened)
    }

    @Test
    fun selectedSettingsAreAnnouncedAndAllSectionsRemainReachable() {
        composeRule.setContent {
            MaterialTheme {
                PlayerSubtitleAppearanceMenu(
                    state = PlayerState(subtitleFontSize = 150, subtitleColor = SUBTITLE_COLOR_CYAN),
                    onSubtitleFontSizeSelect = {},
                    onSubtitleColorSelect = {},
                    onSubtitleBackgroundSelect = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Aparência das legendas").assertIsDisplayed()
        val isCompactWidth = InstrumentationRegistry.getInstrumentation().targetContext
            .resources.configuration.screenWidthDp < 600
        val selectedSize = composeRule.onNodeWithText("150%")
        val selectedColor = composeRule.onNodeWithText("Ciano")
        if (isCompactWidth) {
            selectedSize.performScrollTo()
            selectedColor.performScrollTo()
        }
        selectedSize.assertIsDisplayed()
        selectedColor.assertIsDisplayed()
        val noBackground = composeRule.onNodeWithText("Sem fundo")
        if (isCompactWidth) noBackground.performScrollTo()
        noBackground.assertIsDisplayed()
        composeRule.onNodeWithText("Fechar").assertIsDisplayed()
    }

    @Test
    fun layoutAdaptsPreferenceGroupsToTheDeviceWidth() {
        var screenWidthDp by mutableIntStateOf(800)
        composeRule.setContent {
            val originalConfiguration = LocalConfiguration.current
            val configuration = remember(originalConfiguration, screenWidthDp) {
                Configuration(originalConfiguration).apply { this.screenWidthDp = screenWidthDp }
            }
            CompositionLocalProvider(LocalConfiguration provides configuration) {
                MaterialTheme {
                    PlayerSubtitleAppearanceMenu(
                        state = PlayerState(),
                        onSubtitleFontSizeSelect = {},
                        onSubtitleColorSelect = {},
                        onSubtitleBackgroundSelect = {},
                        onDismiss = {},
                    )
                }
            }
        }

        val sizeHeader = composeRule.onNodeWithText("Tamanho").fetchSemanticsNode().boundsInRoot
        val colorHeader = composeRule.onNodeWithText("Cor").fetchSemanticsNode().boundsInRoot
        val backgroundHeader = composeRule.onNodeWithText("Fundo").fetchSemanticsNode().boundsInRoot
        assertTrue("tablet/TV should place preference groups side by side", colorHeader.left > sizeHeader.left)
        assertTrue("tablet/TV should place the background group in the third column", backgroundHeader.left > colorHeader.left)

        composeRule.runOnIdle { screenWidthDp = 400 }
        composeRule.waitForIdle()
        val stackedSizeHeader = composeRule.onNodeWithText("Tamanho").fetchSemanticsNode().boundsInRoot
        val stackedColorHeader = composeRule.onNodeWithText("Cor").fetchSemanticsNode().boundsInRoot
        assertTrue("phone should stack preference groups vertically", stackedColorHeader.top > stackedSizeHeader.top)
    }

}
