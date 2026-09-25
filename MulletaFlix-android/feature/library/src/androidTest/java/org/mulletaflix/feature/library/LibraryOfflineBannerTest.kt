package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.requestFocus
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** Keeps the library's offline state visible and recoverable on handheld and TV layouts. */
class LibraryOfflineBannerTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun showsOfflineStateAndRetryAction() {
        var retried = false
        composeRule.setContent {
            LibraryOfflineBannerTestSurface(onRetry = { retried = true })
        }

        composeRule
            .onNodeWithText("Sem conexão. A biblioteca será atualizada quando a rede voltar.")
            .assertIsDisplayed()
        composeRule.onNodeWithText("Tentar novamente").assertIsDisplayed().performClick()

        composeRule.runOnIdle { assertTrue(retried) }
    }

    @Test
    fun tvRetryCanReceiveRemoteFocus() {
        composeRule.setContent {
            LibraryOfflineBannerTestSurface(onRetry = {})
        }

        val retry = composeRule.onNodeWithText("Tentar novamente")
        retry.requestFocus()
        retry.assertIsFocused()
    }

    @Test
    fun contextualMessageRemainsVisibleForFavorites() {
        composeRule.setContent {
            MaterialTheme {
                LibraryOfflineBanner(
                    message = "Sem conexão. Minha Lista será atualizada quando a rede voltar.",
                    onRetry = {},
                )
            }
        }

        composeRule
            .onNodeWithText("Sem conexão. Minha Lista será atualizada quando a rede voltar.")
            .assertIsDisplayed()
        composeRule.onNodeWithText("Tentar novamente").assertIsDisplayed()
    }
}

@Composable
private fun LibraryOfflineBannerTestSurface(onRetry: () -> Unit) {
    MaterialTheme {
        LibraryOfflineBanner(onRetry = onRetry)
    }
}
