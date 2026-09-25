package org.mulletaflix.feature.home

import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/** Regression coverage for Home retry feedback on an Android TV remote. */
@RunWith(AndroidJUnit4::class)
class HomeLoadErrorCardTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun tvRetryIsFocusableAndInvokesCallback() {
        assumeTelevisionProfile()

        var retries = 0
        composeRule.setContent {
            MulletaFlixTheme {
                HomeLoadErrorCard(
                    title = "Falha na Home",
                    message = "Conexão temporariamente indisponível",
                    isTelevision = true,
                    onRetry = { retries++ },
                )
            }
        }

        val retry = composeRule.onNodeWithText("Tentar novamente")
        retry.requestFocus()
        retry.assertIsFocused()
        retry.performClick()

        composeRule.runOnIdle { assertEquals(1, retries) }
    }
}
