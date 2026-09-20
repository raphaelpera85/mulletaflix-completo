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
}
