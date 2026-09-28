package org.mulletaflix.feature.downloads

import android.content.res.Configuration
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsOff
import androidx.compose.ui.test.assertIsOn
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

class DownloadsBatchSelectionTvTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun completedRowCanBeSelectedWithTheTvRemoteCenterKey() {
        val uiMode = InstrumentationRegistry.getInstrumentation()
            .targetContext.resources.configuration.uiMode
        assumeTrue(
            "D-pad focus validation applies only to Android TV",
            uiMode and Configuration.UI_MODE_TYPE_MASK == Configuration.UI_MODE_TYPE_TELEVISION,
        )

        var selected by mutableStateOf(false)
        composeRule.setContent {
            MaterialTheme {
                DownloadRow(
                    entry = DownloadEntry(
                        id = "movie",
                        title = "Filme",
                        uri = "https://server/movie",
                        state = DownloadState.Completed,
                        percent = 100,
                    ),
                    imageModel = null,
                    focusFriendly = true,
                    selectionMode = true,
                    isSelected = selected,
                    onPlay = {},
                    onRetry = {},
                    onRemove = {},
                    onToggleSelected = { selected = !selected },
                )
            }
        }

        val row = composeRule.onNodeWithContentDescription("Selecionar download Filme")
        row.assertIsOff().requestFocus().assertIsFocused()
        row.performKeyInput { pressKey(androidx.compose.ui.input.key.Key.DirectionCenter) }
        composeRule.onNodeWithContentDescription("Desmarcar download Filme").assertIsOn()
    }
}
