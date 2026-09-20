package org.mulletaflix.feature.player

import androidx.media3.common.PlaybackException
import org.junit.Assert.assertEquals
import org.junit.Test

class PlaybackErrorPolicyTest {

    @Test
    fun `network failures explain that retry may recover playback`() {
        assertEquals(
            "A conexão com o servidor foi interrompida. Verifique a rede e tente novamente.",
            userFacingPlaybackError(
                PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT,
                "timeout técnico",
            ),
        )
    }

    @Test
    fun `decoder failures explain device incompatibility`() {
        assertEquals(
            "Este dispositivo não conseguiu decodificar o formato desta mídia.",
            userFacingPlaybackError(PlaybackException.ERROR_CODE_DECODING_FAILED, null),
        )
    }

    @Test
    fun `unknown failures retain a useful server message`() {
        assertEquals(
            "Falha de teste",
            userFacingPlaybackError(PlaybackException.ERROR_CODE_UNSPECIFIED, "Falha de teste"),
        )
        assertEquals(
            "Não foi possível reproduzir esta mídia.",
            userFacingPlaybackError(PlaybackException.ERROR_CODE_UNSPECIFIED, "  "),
        )
    }
}
