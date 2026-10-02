package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.swipeUp
import org.junit.Rule
import org.junit.Test

class PlaybackStatsDialogTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun longTechnicalDetailsRemainReachableByScrolling() {
        val longCodec = "Codec final: AV1 / perfil profissional / 10-bit / HDR / faixa estendida"

        composeRule.setContent {
            MaterialTheme {
                PlaybackStatsDialog(
                    title = "Filme de teste",
                    stats = PlaybackStats(
                        videoCodec = longCodec,
                        audioCodec = "E-AC-3 / Atmos / idioma Português (Brasil)",
                        resolution = "3840x2160 UHD",
                        bitrate = "25 Mbps",
                        playMethod = "Direct Play",
                    ),
                    onCopy = {},
                    onShare = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Filme de teste", substring = true).assertExists()
        composeRule
            .onNodeWithContentDescription(PLAYBACK_STATS_CONTENT_DESCRIPTION)
            .performTouchInput { swipeUp() }
        composeRule.onNodeWithText(longCodec, substring = true).assertExists()
    }

    @Test
    fun copyActionIsExposedForTechnicalDetails() {
        var copyCount = 0

        composeRule.setContent {
            MaterialTheme {
                PlaybackStatsDialog(
                    stats = PlaybackStats(videoCodec = "H.265"),
                    onCopy = { copyCount++ },
                    onShare = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Copiar").performClick()
        org.junit.Assert.assertEquals(1, copyCount)
        composeRule.onNodeWithText("Copiado").assertExists()
    }

    @Test
    fun shareActionIsExposedForTechnicalDetails() {
        var shareCount = 0

        composeRule.setContent {
            MaterialTheme {
                PlaybackStatsDialog(
                    stats = PlaybackStats(videoCodec = "H.265"),
                    onCopy = {},
                    onShare = { shareCount++ },
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Compartilhar").performClick()
        org.junit.Assert.assertEquals(1, shareCount)
    }

    @Test
    fun sessionMetricsAreShown() {
        composeRule.setContent {
            MaterialTheme {
                PlaybackStatsDialog(
                    stats = PlaybackStats(
                        sessionMetrics = PlaybackSessionMetrics(
                            firstVideoFrameMs = 720L,
                            bufferingEpisodes = 1,
                            bufferingDurationMs = 1_300L,
                            droppedVideoFrames = 3,
                            activeVideoFormat = "1920x1080 · avc1.640028 · 8000 kbps",
                        ),
                    ),
                    onCopy = {},
                    onShare = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Primeiro quadro de vídeo: 720 ms").assertExists()
        composeRule.onNodeWithText("Interrupções em buffer: 1").assertExists()
        composeRule.onNodeWithText("Quadros de vídeo perdidos: 3").assertExists()
    }

    @Test
    fun castMetricsAreExplicitlyUnavailable() {
        composeRule.setContent {
            MaterialTheme {
                PlaybackStatsDialog(
                    stats = PlaybackStats(videoCodec = "H.265"),
                    isCasting = true,
                    onCopy = {},
                    onShare = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Diagnóstico local indisponível durante transmissão Cast").assertExists()
        composeRule.onNodeWithText("Quadros de vídeo perdidos: indisponível").assertDoesNotExist()
    }
}
