package org.mulletaflix.feature.player

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.util.concurrent.atomic.AtomicInteger
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

@RunWith(AndroidJUnit4::class)
class PlayerNextEpisodePromptTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun televisionPromptFocusesPlayActionAndInvokesCallback() {
        assumeTelevisionProfile()
        val plays = AtomicInteger(0)
        composeRule.setContent {
            MulletaFlixTheme {
                PlayerNextEpisodePrompt(
                    nextEpisode = NextEpisodeInfo("episode-2", "Segundo episódio", 2, 1),
                    countdownSeconds = 3,
                    isTelevision = true,
                    onCancel = {},
                    onPlayNow = { plays.incrementAndGet() },
                )
            }
        }

        composeRule.onNodeWithText("Próximo Episódio").assertIsDisplayed()
        composeRule.onNodeWithText("Assistir Agora").assertIsFocused().performClick()
        composeRule.runOnIdle { assertEquals(1, plays.get()) }
    }
}
