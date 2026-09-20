package org.mulletaflix.feature.settings

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class ClearImageCacheDialogTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun cancelDoesNotClearCacheAndConfirmClearsOnce() {
        var clearCount = 0
        composeRule.setContent {
            MaterialTheme {
                ClearImageCacheDialog(
                    onConfirm = { clearCount++ },
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Limpar cache de imagens?").assertExists()
        composeRule.onNodeWithText("Cancelar").performClick()
        assertEquals(0, clearCount)

        composeRule.onNodeWithText("Limpar").performClick()
        assertEquals(1, clearCount)
    }
}
