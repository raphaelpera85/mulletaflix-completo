package org.mulletaflix.feature.player

import android.content.res.Configuration
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class PlayerSubtitleAppearanceTvDpadTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun dpadMovesBetweenSubtitleAppearanceChoicesOnAndroidTv() {
        val configuration = InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration
        val deviceType = configuration.uiMode and Configuration.UI_MODE_TYPE_MASK
        assertEquals("This test must run on Android TV", Configuration.UI_MODE_TYPE_TELEVISION, deviceType)

        composeRule.setContent {
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

        val firstSize = composeRule.onNode(hasText("50%") and hasClickAction())
        firstSize.requestFocus().assertIsFocused()
        firstSize.performKeyInput { pressKey(androidx.compose.ui.input.key.Key.DirectionDown) }
        composeRule.onNode(hasText("75%") and hasClickAction()).assertIsFocused()
    }
}
