package org.mulletaflix.feature.livetv

import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.requestFocus
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

/** Compose coverage for the EPG body used by touch, tablet and TV surfaces. */
@RunWith(AndroidJUnit4::class)
class LiveTvGuideContentTest {
    @get:Rule
    val composeRule = createComposeRule()

    private val programme = MediaItem(
        id = "programme-1",
        name = "Filme das 20h",
        type = MediaItemType.LiveTvProgram,
        overview = "Programa de teste",
        channelId = "channel-1",
        startDate = "2026-09-24T20:00:00Z",
        endDate = "2026-09-24T22:00:00Z",
    )

    @Test
    fun schedulableProgrammeExposesActionAndInvokesCallback() {
        var scheduledId: String? = null
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(programs = listOf(programme)),
                    isTelevision = true,
                    onSchedule = { scheduledId = it.id },
                    onCancel = {},
                    onRetry = {},
                    onRetryTimerLookup = {},
                )
            }
        }

        composeRule.onNodeWithText("Filme das 20h").assertIsDisplayed()
        composeRule.onNodeWithText("Gravar").performClick()
        composeRule.runOnIdle { assertEquals("programme-1", scheduledId) }
    }

    @Test
    fun scheduledProgrammeIsNotPresentedAsRecordAgain() {
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(
                        programs = listOf(programme),
                        scheduledProgramIds = setOf("programme-1"),
                    ),
                    onSchedule = {},
                    onCancel = {},
                    onRetry = {},
                    onRetryTimerLookup = {},
                )
            }
        }

        composeRule.onNodeWithText("Agendado").assertIsDisplayed()
        composeRule.onAllNodesWithText("Gravar").assertCountEquals(0)
    }

    @Test
    fun scheduledProgrammeWithTimerExposesFocusedCancelAction() {
        var cancelledId: String? = null
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(
                        programs = listOf(programme),
                        scheduledProgramIds = setOf("programme-1"),
                        scheduledProgramTimerIds = mapOf("programme-1" to "timer-1"),
                    ),
                    isTelevision = true,
                    onSchedule = {},
                    onCancel = { cancelledId = it.id },
                    onRetry = {},
                    onRetryTimerLookup = {},
                )
            }
        }

        val cancel = composeRule.onNodeWithText("Cancelar")
        cancel.assertIsDisplayed()
        cancel.requestFocus()
        cancel.assertIsFocused()
        cancel.performClick()
        composeRule.runOnIdle { assertEquals("programme-1", cancelledId) }
    }

    @Test
    fun guideErrorKeepsProgrammeVisibleAndOffersRetry() {
        var retries = 0
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(
                        programs = listOf(programme),
                        guideError = "Falha temporária",
                    ),
                    isTelevision = true,
                    onSchedule = {},
                    onCancel = {},
                    onRetry = { retries++ },
                    onRetryTimerLookup = {},
                )
            }
        }

        composeRule.onNodeWithText("Falha temporária").assertIsDisplayed()
        composeRule.onNodeWithText("Filme das 20h").assertIsDisplayed()
        val retry = composeRule.onNodeWithText("Tentar novamente")
        retry.requestFocus()
        retry.assertIsFocused()
        retry.performClick()
        composeRule.runOnIdle { assertEquals(1, retries) }
    }

    @Test
    fun scheduledProgrammeWithoutTimerOffersFocusedVerificationAction() {
        var verifiedId: String? = null
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(
                        programs = listOf(programme),
                        scheduledProgramIds = setOf("programme-1"),
                        locallyScheduledProgramIds = setOf("programme-1"),
                    ),
                    isTelevision = true,
                    onSchedule = {},
                    onCancel = {},
                    onRetry = {},
                    onRetryTimerLookup = { verifiedId = it.id },
                )
            }
        }

        composeRule.onNodeWithText("Aguardando confirmação do servidor").assertIsDisplayed()
        val verify = composeRule.onNodeWithText("Verificar")
        verify.requestFocus()
        verify.assertIsFocused()
        verify.performClick()
        composeRule.runOnIdle { assertEquals("programme-1", verifiedId) }
    }

    @Test
    fun scheduledProgrammeActionsAreDisabledOffline() {
        var verified = false
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(
                        programs = listOf(programme),
                        scheduledProgramIds = setOf("programme-1"),
                        locallyScheduledProgramIds = setOf("programme-1"),
                        isOffline = true,
                    ),
                    isTelevision = true,
                    onSchedule = {},
                    onCancel = {},
                    onRetry = {},
                    onRetryTimerLookup = { verified = true },
                )
            }
        }

        composeRule.onNodeWithText("Verificar").assertIsNotEnabled()
        composeRule.onNodeWithText("Verificar").performClick()
        composeRule.runOnIdle { assertEquals(false, verified) }
    }

    @Test
    fun scheduledProgrammeCannotBeCancelledOffline() {
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(
                        programs = listOf(programme),
                        scheduledProgramIds = setOf("programme-1"),
                        scheduledProgramTimerIds = mapOf("programme-1" to "timer-1"),
                        isOffline = true,
                    ),
                    isTelevision = true,
                    onSchedule = {},
                    onCancel = {},
                    onRetry = {},
                    onRetryTimerLookup = {},
                )
            }
        }

        composeRule.onNodeWithText("Cancelar").assertIsNotEnabled()
    }

    @Test
    fun unscheduledProgrammeCannotBeScheduledOffline() {
        composeRule.setContent {
            MulletaFlixTheme {
                GuideContent(
                    state = LiveTvUiState(programs = listOf(programme), isOffline = true),
                    isTelevision = true,
                    onSchedule = {},
                    onCancel = {},
                    onRetry = {},
                    onRetryTimerLookup = {},
                )
            }
        }

        composeRule.onNodeWithText("Gravar").assertIsNotEnabled()
    }
}
