package org.mulletaflix.feature.home

import android.view.KeyEvent
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextInput
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaSuggestion
import java.util.concurrent.atomic.AtomicReference

class MediaRequestDialogTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun requestRequiresTitleAndRejectsYearsOutsideSupportedRange() {
        showDialog()

        composeRule.onNodeWithText("Enviar").assertIsNotEnabled()
        composeRule.onNodeWithTag("media-request-title").performTextInput("Duna")
        composeRule.onNodeWithTag("media-request-year").performTextInput("1800")

        composeRule.onNodeWithText("Informe um ano entre 1888 e 2200.").assertIsDisplayed()
        composeRule.onNodeWithText("Enviar").assertIsNotEnabled()
    }

    @Test
    fun requestSubmissionWaitsForAnAvailableSession() {
        composeRule.setContent {
            MulletaFlixTheme {
                MediaRequestDialog(
                    title = "Duna",
                    onTitleChange = {},
                    mediaType = "Filme",
                    onMediaTypeChange = {},
                    year = "2024",
                    onYearChange = {},
                    notes = "",
                    onNotesChange = {},
                    message = null,
                    isSubmitting = false,
                    hasFeedbackSession = false,
                    feedbackSessionLoaded = false,
                    onDismiss = {},
                    onSubmit = {},
                )
            }
        }

        composeRule.onNodeWithText("Verificando sessão…").assertIsDisplayed()
        composeRule.onNodeWithText("Enviar").assertIsNotEnabled()
    }

    @Test
    fun validRequestSubmitsSelectedTypeYearAndDetails() {
        var submitted: List<String?>? = null
        composeRule.setContent {
            MulletaFlixTheme {
                var title by remember { mutableStateOf("") }
                var type by remember { mutableStateOf("Série") }
                var year by remember { mutableStateOf("") }
                var notes by remember { mutableStateOf("") }
                MediaRequestDialog(
                    title = title,
                    onTitleChange = { title = it },
                    mediaType = type,
                    onMediaTypeChange = { type = it },
                    year = year,
                    onYearChange = { year = it },
                    notes = notes,
                    onNotesChange = { notes = it },
                    message = null,
                    isSubmitting = false,
                    hasFeedbackSession = true,
                    feedbackSessionLoaded = true,
                    onDismiss = {},
                    onSubmit = { submitted = listOf(title, type, year, notes) },
                )
            }
        }

        composeRule.onNodeWithTag("media-request-title").performTextInput("Duna")
        composeRule.onNodeWithTag("media-request-year").performTextInput("2024")
        composeRule.onNodeWithTag("media-request-notes").performTextInput("Áudio em português")
        composeRule.onNodeWithText("Série").performClick()
        composeRule.onNodeWithText("Filme").performClick()
        composeRule.onNodeWithText("Enviar").assertIsEnabled().performClick()

        composeRule.runOnIdle {
            assertEquals(listOf("Duna", "Filme", "2024", "Áudio em português"), submitted)
        }
    }

    @Test
    fun submissionErrorRemainsVisibleAndFieldsStayAvailableForRetry() {
        val editedTitle = AtomicReference<String>()
        composeRule.setContent {
            MulletaFlixTheme {
                var title by remember { mutableStateOf("Duna") }
                MediaRequestDialog(
                    title = title,
                    onTitleChange = { title = it; editedTitle.set(it) },
                    mediaType = "Filme",
                    onMediaTypeChange = {},
                    year = "2024",
                    onYearChange = {},
                    notes = "Legendas",
                    onNotesChange = {},
                    message = "Falha de conexão. Tente novamente.",
                    isSubmitting = false,
                    hasFeedbackSession = true,
                    feedbackSessionLoaded = true,
                    onDismiss = {},
                    onSubmit = {},
                )
            }
        }

        composeRule.onNodeWithText("Falha de conexão. Tente novamente.").assertIsDisplayed()
        composeRule.onNodeWithTag("media-request-title").assertIsEnabled().performTextInput(" (2024)")
        composeRule.onNodeWithTag("media-request-year").assertIsEnabled()
        composeRule.onNodeWithTag("media-request-notes").assertIsEnabled()
        composeRule.onNodeWithText("Enviar").assertIsEnabled()
        composeRule.runOnIdle {
            assertTrue(editedTitle.get().contains("Duna"))
            assertTrue(editedTitle.get().contains("(2024)"))
        }
    }

    @Test
    fun submittingDisablesFieldsAndCancelAction() {
        var dismissCount = 0
        composeRule.setContent {
            MulletaFlixTheme {
                MediaRequestDialog(
                    title = "Duna",
                    onTitleChange = {},
                    mediaType = "Filme",
                    onMediaTypeChange = {},
                    year = "2024",
                    onYearChange = {},
                    notes = "Legendas",
                    onNotesChange = {},
                    message = "Enviando solicitação…",
                    isSubmitting = true,
                    hasFeedbackSession = true,
                    feedbackSessionLoaded = true,
                    onDismiss = { dismissCount++ },
                    onSubmit = {},
                )
            }
        }

        composeRule.onNodeWithTag("media-request-title").assertIsNotEnabled()
        composeRule.onNodeWithTag("media-request-year").assertIsNotEnabled()
        composeRule.onNodeWithTag("media-request-notes").assertIsNotEnabled()
        composeRule.onNodeWithText("Cancelar").assertIsNotEnabled()

        InstrumentationRegistry.getInstrumentation().sendKeyDownUpSync(KeyEvent.KEYCODE_BACK)

        composeRule.onNodeWithText("Solicitar mídia").assertIsDisplayed()
        composeRule.runOnIdle { assertEquals(0, dismissCount) }
    }

    @Test
    fun longSuggestionListKeepsLowerFieldsScrollableAndSubmitActionReachable() {
        var submitted = false
        composeRule.setContent {
            MulletaFlixTheme {
                MediaRequestDialog(
                    title = "Duna",
                    onTitleChange = {},
                    suggestions = List(12) { index ->
                        MediaSuggestion(
                            title = "Duna - resultado ${index + 1}",
                            mediaType = "Movie",
                            year = 2021 + index,
                        )
                    },
                    mediaType = "Filme",
                    onMediaTypeChange = {},
                    year = "2024",
                    onYearChange = {},
                    notes = "",
                    onNotesChange = {},
                    message = null,
                    isSubmitting = false,
                    hasFeedbackSession = true,
                    feedbackSessionLoaded = true,
                    onDismiss = {},
                    onSubmit = { submitted = true },
                )
            }
        }

        composeRule.onNodeWithTag("media-request-notes").performScrollTo().assertIsDisplayed()
        composeRule.onNodeWithText("Enviar").assertIsDisplayed().performClick()
        composeRule.runOnIdle { assertTrue(submitted) }
    }

    private fun showDialog() {
        composeRule.setContent {
            MulletaFlixTheme {
                var title by remember { mutableStateOf("") }
                var year by remember { mutableStateOf("") }
                MediaRequestDialog(
                    title = title,
                    onTitleChange = { title = it },
                    mediaType = "Série",
                    onMediaTypeChange = {},
                    year = year,
                    onYearChange = { year = it },
                    notes = "",
                    onNotesChange = {},
                    message = null,
                    isSubmitting = false,
                    hasFeedbackSession = true,
                    feedbackSessionLoaded = true,
                    onDismiss = {},
                    onSubmit = {},
                )
            }
        }
    }
}
