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

/** Regression coverage for player recovery on touch and remote-control surfaces. */
@RunWith(AndroidJUnit4::class)
class PlayerErrorCardTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun televisionRetryActionIsVisibleAndInvokesCallback() {
        assumeTelevisionProfile()
        val retries = AtomicInteger(0)
        composeRule.setContent {
            MulletaFlixTheme {
                PlayerErrorCard(
                    message = "Falha temporária",
                    isTelevision = true,
                    onRetry = { retries.incrementAndGet() },
                )
            }
        }

        composeRule.onNodeWithText("Falha temporária").assertIsDisplayed()
        composeRule.onNodeWithText("Tentar novamente").assertIsFocused()
        composeRule.onNodeWithText("Tentar novamente").performClick()
        composeRule.runOnIdle { assertEquals(1, retries.get()) }
    }
}
