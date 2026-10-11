package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.io.IOException

class PlaybackIssueReportDialogTest {
    @get:Rule val composeRule = createComposeRule()

    @Test
    fun failedOnlineSendOffersQueueAndSavesTheUserReport() {
        var submittedCategory: String? = null
        var submittedDescription: String? = null
        var queuedCategory: String? = null
        var queuedDescription: String? = null
        var dismissed = false

        composeRule.setContent {
            MaterialTheme {
                PlaybackIssueReportDialog(
                    hasFeedbackSession = true,
                    feedbackSessionLoaded = true,
                    onDismiss = { dismissed = true },
                    onSubmit = { category, description, complete ->
                        submittedCategory = category
                        submittedDescription = description
                        complete(Result.failure(IOException("Sem conexão de rede")))
                    },
                    onQueue = { category, description, complete ->
                        queuedCategory = category
                        queuedDescription = description
                        complete(Result.success(Unit))
                    },
                )
            }
        }

        composeRule.onNodeWithText("Descreva o problema (opcional)")
            .performTextInput("  imagem trava  ")
        composeRule.onNodeWithText("Enviar").performClick()
        composeRule.waitUntil(5_000) {
            composeRule.onAllNodesWithTag("playback-issue-queue").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithTag("playback-issue-queue").assertIsDisplayed().performClick()

        assertEquals("Não reproduz", submittedCategory)
        assertEquals("  imagem trava  ", submittedDescription)
        assertEquals("Não reproduz", queuedCategory)
        assertEquals("  imagem trava  ", queuedDescription)
        assertTrue(dismissed)
    }

    @Test
    fun queueActionIsHiddenUntilAReportSendFails() {
        composeRule.setContent {
            MaterialTheme {
                PlaybackIssueReportDialog(
                    hasFeedbackSession = true,
                    feedbackSessionLoaded = true,
                    onDismiss = {},
                    onSubmit = { _, _, _ -> },
                    onQueue = { _, _, _ -> },
                )
            }
        }

        composeRule.onNodeWithTag("playback-issue-queue").assertDoesNotExist()
    }
}
