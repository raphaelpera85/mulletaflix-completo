package org.mulletaflix.feature.player

import androidx.activity.ComponentActivity
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.getOrElse
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.isFocused
import androidx.compose.ui.test.onAllNodes
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.media3.common.util.UnstableApi
import androidx.test.platform.app.InstrumentationRegistry
import com.google.android.gms.cast.framework.CastContext
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * Navegacao por controle remoto (D-pad) dentro do OSD do player, na TV.
 *
 * Historico: na TV, o container raiz do `VideoPlayerScreen` era `clickable` + segurava o
 * foco inicial. O foco ficava **preso** nele: a busca direcional do Compose nao enxerga os
 * filhos de um pai focado, entao so pausar/avançar/retroceder respondiam (via teclas de
 * midia), e os botoes de audio, legendas, qualidade, velocidade etc. apareciam na tela sem
 * nunca receber foco pelo D-pad.
 *
 * Estes testes reproduzem a estrutura real (raiz com foco + OSD revelado por D-pad) e
 * provam o comportamento desejado:
 *  1. revelar o OSD com CENTER move o foco para o botao Pausar;
 *  2. a partir do Pausar, CIMA alcanca um botao da barra superior e BAIXO alcanca a barra
 *     de busca.
 */
@androidx.annotation.OptIn(UnstableApi::class)
@OptIn(UnstableApi::class)
class PlayerOsdRemoteNavigationTest {

    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Before
    fun initializeCast() {
        assumeTelevisionProfile()
        // O MediaRouteButton exige o CastContext na thread principal, exatamente como em
        // PlayerCastControlTouchTargetTest.
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            runCatching { CastContext.getSharedInstance(it) }
        }
    }

    private fun setTelevisionPlayerContent() {
        composeRule.setContent {
            MaterialTheme {
                var osdVisible by remember { mutableStateOf(false) }
                val rootFocusRequester = remember { FocusRequester() }
                LaunchedEffect(Unit) { rootFocusRequester.requestFocus() }
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        // Mesma cadeia de modificadores que o VideoPlayerScreen aplica no
                        // perfil de TV: o teste replica a camada real de input.
                        .focusRequester(rootFocusRequester)
                        .onPreviewKeyEvent { event ->
                            if (event.type != KeyEventType.KeyDown || !isPlayerOsdRevealKey(event.key)) {
                                return@onPreviewKeyEvent false
                            }
                            if (!osdVisible) {
                                osdVisible = true
                                true
                            } else {
                                false
                            }
                        }
                        .clickable(
                            interactionSource = null,
                            indication = null,
                            onClick = { if (!osdVisible) osdVisible = true },
                        ),
                ) {
                    if (osdVisible) {
                        PlayerOsd(
                            state = PlayerState(
                                title = "Filme de teste",
                                isPlaying = true,
                                currentPosition = 10_000L,
                                duration = 60_000L,
                                isSeekable = true,
                                audioTracks = listOf(TrackInfo(index = 1, displayName = "Portugues")),
                                subtitleTracks = listOf(TrackInfo(index = 2, displayName = "Portugues (CC)")),
                            ),
                            onBack = {},
                            onPlayPause = {},
                            onSeekPreview = {},
                            onSeekFinished = {},
                            onSeekBy = {},
                            onPrevious = {},
                            onNext = {},
                            onPreviousChapter = {},
                            onNextChapter = {},
                            onSubtitleSelect = {},
                            onAudioSelect = {},
                            onQualitySelect = {},
                            onSpeedSelect = {},
                            onSleepTimerSelect = {},
                            onSleepTimerAtMediaEnd = {},
                            onAspectRatioSelect = {},
                            onLockClick = {},
                            onCastClick = {},
                            onCopyStats = {},
                            onShareStats = {},
                        )
                    }
                }
            }
        }
        composeRule.waitForIdle()
    }

    private fun focusedContentDescriptions(): List<String> =
        composeRule.onAllNodes(isFocused(), useUnmergedTree = true)
            .fetchSemanticsNodes()
            .flatMap { it.config.getOrElse(SemanticsProperties.ContentDescription) { emptyList() } }

    @Test
    fun tvRevealOsdMovesFocusToPauseButton() {
        setTelevisionPlayerContent()

        composeRule.onRoot().performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.waitForIdle()

        composeRule.onNodeWithContentDescription("Pausar").assertIsFocused()
    }

    @Test
    fun tvDpadFromPauseReachesTopBarActionsAndSeekBar() {
        setTelevisionPlayerContent()
        composeRule.onRoot().performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.waitForIdle()
        composeRule.onNodeWithContentDescription("Pausar").assertIsFocused()

        // CIMA a partir do Pausar precisa alcancar um botao da barra superior
        // (Voltar, Proporcao, Audio, Legendas, Qualidade, Velocidade etc.).
        composeRule.onRoot().performKeyInput { pressKey(Key.DirectionUp) }
        composeRule.waitForIdle()
        val topBarDescriptions = setOf(
            "Voltar", "Proporção", "Áudio", "Legendas", "Qualidade", "Velocidade",
            "Temporizador de suspensão", "Estatísticas", "Bloquear controles",
            "Transmitir", "Cast", "Cast devices",
        )
        val focused = focusedContentDescriptions()
        assertTrue(
            "D-pad CIMA deveria alcançar a barra superior, mas o foco ficou em $focused",
            focused.any { candidate ->
                topBarDescriptions.any { expected -> candidate.contains(expected, ignoreCase = true) }
            },
        )

        // Volta ao centro e desce ate a barra de busca.
        composeRule.onRoot().performKeyInput { pressKey(Key.DirectionDown) }
        composeRule.waitForIdle()
        composeRule.onNodeWithContentDescription("Pausar").assertIsFocused()
        composeRule.onRoot().performKeyInput { pressKey(Key.DirectionDown) }
        composeRule.waitForIdle()
        composeRule.onNodeWithContentDescription("Posição da reprodução").assertIsFocused()
    }
}
