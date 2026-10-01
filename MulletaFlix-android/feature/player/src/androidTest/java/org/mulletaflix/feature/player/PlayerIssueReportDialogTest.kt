package org.mulletaflix.feature.player

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import java.util.concurrent.atomic.AtomicReference
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

@RunWith(AndroidJUnit4::class)
class PlayerIssueReportDialogTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun selectsIssueCategoryAndSubmitsDescription() {
        val submitted = AtomicReference<Pair<String, String?>>()
        composeRule.setContent {
            MulletaFlixTheme {
                PlayerIssueReportDialog(
                    initialDescription = null,
                    isSubmitting = false,
                    isSent = false,
                    message = null,
                    onDismiss = {},
                    onSubmit = { category, description -> submitted.set(category to description) },
                )
            }
        }

        composeRule.onNodeWithText("Não reproduz").performClick()
        composeRule.onNodeWithText("Sem áudio").performClick()
        composeRule.onNodeWithText("Descreva o problema (opcional)").performTextInput("Faixa em português sem som")
        composeRule.onNodeWithText("Enviar").performClick()

        composeRule.runOnIdle {
            assertEquals("Sem áudio", submitted.get().first)
            assertEquals("Faixa em português sem som", submitted.get().second)
        }
    }

    @Test
    fun showsSentFeedbackAndDisablesDuplicateSubmission() {
        composeRule.setContent {
            MulletaFlixTheme {
                PlayerIssueReportDialog(
                    initialDescription = "Falha",
                    isSubmitting = false,
                    isSent = true,
                    message = "Relato enviado. Obrigado pelo aviso.",
                    onDismiss = {},
                    onSubmit = { _, _ -> error("Sent reports cannot be submitted again") },
                )
            }
        }

        composeRule.onNodeWithText("Relato enviado. Obrigado pelo aviso.").assertIsDisplayed()
        composeRule.onNodeWithText("Enviado").assertIsNotEnabled()
    }

    @Test
    fun showsOfflineQueuedFeedbackAndPreventsDuplicateSubmission() {
        composeRule.setContent {
            MulletaFlixTheme {
                PlayerIssueReportDialog(
                    initialDescription = "Sem rede",
                    isSubmitting = false,
                    isSent = false,
                    isQueued = true,
                    message = "Relato salvo. Será enviado quando houver conexão com o servidor.",
                    onDismiss = {},
                    onSubmit = { _, _ -> error("Queued reports cannot be submitted again") },
                )
            }
        }

        composeRule.onNodeWithText("Relato salvo. Será enviado quando houver conexão com o servidor.").assertIsDisplayed()
        composeRule.onNodeWithText("Na fila").assertIsNotEnabled()
    }

    @Test
    fun longFeedbackMessageDoesNotHideDescriptionOrDialogActions() {
        val longMessage = "Falha temporária ao enviar. ".repeat(80)
        composeRule.setContent {
            MulletaFlixTheme {
                PlayerIssueReportDialog(
                    initialDescription = null,
                    isSubmitting = false,
                    isSent = false,
                    message = longMessage,
                    onDismiss = {},
                    onSubmit = { _, _ -> },
                )
            }
        }

        composeRule.onNodeWithTag("playback-issue-content").assertIsDisplayed()
        composeRule.onNodeWithText("Descreva o problema (opcional)").performScrollTo().assertIsDisplayed()
        composeRule.onNodeWithText("Enviar").assertIsDisplayed()
        composeRule.onNodeWithText("Fechar").assertIsDisplayed()
    }
}
