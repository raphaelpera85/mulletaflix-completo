package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class LibraryOfflineMediaPreviewTvTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun offlinePreviewCanBeDismissedWithTheRemote() {
        assumeTelevisionProfile()
        var dismissed = false
        composeRule.setContent {
            MaterialTheme {
                OfflineMediaPreviewDialog(
                    preview = LibraryOfflineMediaPreview.from(
                        MediaItem("movie-tv", "Filme na TV", MediaItemType.Movie),
                    ),
                    isOffline = true,
                    onDismiss = { dismissed = true },
                    onOpenDetails = {},
                )
            }
        }

        val closeButton = composeRule.onNodeWithText("Fechar")
        closeButton.requestFocus()
        closeButton.assertIsFocused()
        closeButton.performKeyInput { pressKey(Key.Enter) }
        composeRule.runOnIdle { assertTrue(dismissed) }
    }
}
