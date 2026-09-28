package org.mulletaflix.feature.downloads

import android.content.res.Configuration
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.DownloadState
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase

class DownloadsTvScreenTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun tvDownloadsScreenWiringHidesRestartActions() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val isTelevision = (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        if (!isTelevision) return

        val repository = TvQueueRepository(
            entries = listOf(
                DownloadEntry("queued", "Na fila", "https://server/queued", DownloadState.Queued, 20),
                DownloadEntry("failed", "Falhou", "https://server/failed", DownloadState.Failed, 10),
            ),
        )
        val viewModel = DownloadsViewModel(ManageDownloadsUseCase(repository))
        composeRule.setContent {
            MaterialTheme {
                DownloadsScreen(
                    onItemClick = {},
                    onBack = {},
                    viewModel = viewModel,
                )
            }
        }

        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithText("Modo offline").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithContentDescription("Retomar downloads").assertDoesNotExist()
        composeRule.onNodeWithText("Tentar novamente (1 falha(s))").assertDoesNotExist()
        composeRule.onNodeWithContentDescription("Tentar download novamente").assertDoesNotExist()
        composeRule.onNodeWithText("Somente Wi‑Fi").assertDoesNotExist()
    }
}

private class TvQueueRepository(
    private val entries: List<DownloadEntry>,
) : DownloadRepository {
    override fun observeDownloads(): Flow<List<DownloadEntry>> = flowOf(entries)
    override fun observeQueuePaused(): Flow<Boolean> = flowOf(true)
    override fun observeWifiOnly(): Flow<Boolean> = flowOf(true)
    override fun enqueue(id: String, title: String, uri: String) = Result.success(Unit)
    override fun retry(downloadId: String, title: String, uri: String) = Result.success(Unit)
    override fun remove(downloadId: String) = Result.success(Unit)
    override fun pauseAll() = Result.success(Unit)
    override fun resumeAll() = Result.success(Unit)
}
