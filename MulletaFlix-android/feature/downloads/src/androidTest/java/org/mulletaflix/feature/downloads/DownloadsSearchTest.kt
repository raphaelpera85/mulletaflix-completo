package org.mulletaflix.feature.downloads

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
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
    fun queueContentTransitionsFromLoadingToConfirmedEmptyToContent() {
        var state by mutableStateOf(DownloadsUiState())
        var explored = false
        composeRule.setContent {
            MaterialTheme {
                DownloadsQueueContent(
                    state = state,
                    onExploreClick = { explored = true },
                ) {
                    Text("Fila carregada")
                }
            }
        }

        composeRule.onNodeWithText("Carregando downloads offline…").assertExists()
        composeRule.onAllNodesWithText("Nenhum download concluído").assertCountEquals(0)
        composeRule.onAllNodesWithText("Fila carregada").assertCountEquals(0)

        state = DownloadsUiState(isLoaded = true)
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Nenhum download concluído").assertExists()
        composeRule.onNodeWithText("Explorar Catálogo").performClick()
        composeRule.runOnIdle { check(explored) }

        state = DownloadsUiState(
            entries = listOf(DownloadEntry("movie", "Filme", "https://server/movie", DownloadState.Completed, 100)),
            isLoaded = true,
        )
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Fila carregada").assertExists()
        composeRule.onAllNodesWithText("Nenhum download concluído").assertCountEquals(0)
    }

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

    @Test
    fun completedDownloadUsesSingleTvFocusTargetForPlayback() {
        var played = false
        composeRule.setContent {
            MaterialTheme {
                DownloadRow(
                    entry = DownloadEntry(
                        id = "done",
                        title = "Filme",
                        uri = "https://server/done",
                        state = DownloadState.Completed,
                        percent = 100,
                    ),
                    imageModel = null,
                    focusFriendly = true,
                    onPlay = { played = true },
                    onRetry = {},
                    onRemove = {},
                )
            }
        }

        composeRule.onNodeWithContentDescription("Reproduzir Filme offline").performClick()

        composeRule.runOnIdle { check(played) }
    }
}
