package org.mulletaflix.feature.downloads

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

class DownloadsSearchTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun searchFieldShowsClearActionAfterTyping() {
        var query by mutableStateOf("")
        composeRule.setContent {
            MaterialTheme {
                DownloadSearchField(
                    query = query,
                    onQueryChange = { query = it },
                    onClear = {},
                )
            }
        }

        composeRule.onNode(hasSetTextAction()).performTextInput("viagem")

        composeRule.onAllNodesWithContentDescription("Limpar busca").assertCountEquals(1)
    }

    @Test
    fun statusFilterUpdatesSelection() {
        var selected by mutableStateOf(DownloadStatusFilter.All)
        composeRule.setContent {
            MaterialTheme {
                DownloadFilterRow(
                    selectedFilter = selected,
                    onFilterSelected = { selected = it },
                )
            }
        }

        composeRule.onNodeWithText("Falhos").performClick()

        composeRule.onNodeWithText("Falhos").assertExists()
        check(selected == DownloadStatusFilter.Failed)
    }

    @Test
    fun storageSummaryExplainsKnownAndUnknownSizes() {
        composeRule.setContent {
            MaterialTheme {
                StorageSummaryDialog(
                    summary = DownloadStorageSummary(
                        downloadedBytes = 2_000_000,
                        knownContentBytes = 0,
                        itemCount = 1,
                    ),
                    onDismiss = {},
                )
            }
        }

        composeRule.onAllNodesWithText("Armazenamento offline").assertCountEquals(1)
        composeRule.onAllNodesWithText("O tamanho total será informado pelo servidor quando estiver disponível.").assertCountEquals(1)
    }

    @Test
    fun retryAllButtonIsShownOnlyWhenThereAreFailedDownloads() {
        var retryCount = 0
        var downloads by mutableStateOf(
            listOf(
                DownloadEntry(
                    id = "failed",
                    title = "Falhou",
                    uri = "https://server/media",
                    state = DownloadState.Failed,
                    percent = 12,
                ),
            ),
        )
        composeRule.setContent {
            MaterialTheme {
                OfflineSummary(
                    downloads = downloads,
                    queuePaused = false,
                    onPause = {},
                    onResume = {},
                    onRetryFailed = { retryCount++ },
                    onClearCompleted = {},
                    onClearFailed = {},
                    wifiOnly = false,
                    onWifiOnlyChange = {},
                )
            }
        }

        composeRule.onNodeWithText("Tentar novamente (1 falha(s))").performClick()
        check(retryCount == 1)
        downloads = listOf(
            DownloadEntry("done", "Pronto", "https://server/done", DownloadState.Completed, 100),
        )
        composeRule.waitForIdle()
        composeRule.onAllNodesWithText("Tentar novamente (1 falha(s))").assertCountEquals(0)
    }

    @Test
    fun clearFailedButtonIsShownOnlyWhenThereAreFailedDownloads() {
        var clearCount = 0
        var downloads by mutableStateOf(
            listOf(
                DownloadEntry(
                    id = "failed",
                    title = "Falhou",
                    uri = "https://server/media",
                    state = DownloadState.Failed,
                    percent = 12,
                ),
            ),
        )
        composeRule.setContent {
            MaterialTheme {
                OfflineSummary(
                    downloads = downloads,
                    queuePaused = false,
                    onPause = {},
                    onResume = {},
                    onRetryFailed = {},
                    onClearCompleted = {},
                    onClearFailed = { clearCount++ },
                    wifiOnly = false,
                    onWifiOnlyChange = {},
                )
            }
        }

        composeRule.onNodeWithText("Limpar falhas (1)").performClick()
        check(clearCount == 1)
        downloads = listOf(
            DownloadEntry("done", "Pronto", "https://server/done", DownloadState.Completed, 100),
        )
        composeRule.waitForIdle()
        composeRule.onAllNodesWithText("Limpar falhas (1)").assertCountEquals(0)
    }
}
