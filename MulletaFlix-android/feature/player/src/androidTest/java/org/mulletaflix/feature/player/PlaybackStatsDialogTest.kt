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
                    stats = PlaybackStats(
                        videoCodec = longCodec,
                        audioCodec = "E-AC-3 / Atmos / idioma Português (Brasil)",
                        resolution = "3840x2160 UHD",
                        bitrate = "25 Mbps",
                        playMethod = "Direct Play",
                    ),
                    onCopy = {},
                    onDismiss = {},
                )
            }
        }

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
                    onDismiss = {},
                )
            }
        }

        composeRule.onNodeWithText("Copiar").performClick()
        org.junit.Assert.assertEquals(1, copyCount)
        composeRule.onNodeWithText("Copiado").assertExists()
    }
}
