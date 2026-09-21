package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** Ensures a failed foreground refresh remains recoverable with already-loaded content. */
class FavoritesErrorTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun inlineErrorOffersRetryAction() {
        var retried = false
        composeRule.setContent {
            FavoritesErrorTestSurface(onRetry = { retried = true })
        }

        composeRule.onNodeWithText("Não foi possível atualizar Minha Lista.").assertIsDisplayed()
        composeRule.onNodeWithText("Tentar novamente").assertIsDisplayed().performClick()

        composeRule.runOnIdle { assertTrue(retried) }
    }
}

@Composable
private fun FavoritesErrorTestSurface(onRetry: () -> Unit) {
    MaterialTheme {
        FavoritesInlineError(
            message = "Não foi possível atualizar Minha Lista.",
            onRetry = onRetry,
        )
    }
}
