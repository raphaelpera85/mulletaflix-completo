package org.mulletaflix.feature.player

import androidx.media3.common.PlaybackException
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
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

    /**
     * Some Media3 codes put the URL they failed on inside `localizedMessage`, and a
     * playback URL carries the session token as `api_key`. Every code known to do
     * that has a curated message above, so this is the net under the list: a code
     * nobody has mapped yet must not be able to put a token on screen.
     */
    @Test
    fun `an unmapped failure never shows a token`() {
        val message = userFacingPlaybackError(
            PlaybackException.ERROR_CODE_UNSPECIFIED,
            "Failed to open http://192.168.15.9:8096/Videos/x/stream?api_key=super-secret&Static=true",
        )
        assertFalse("the session token reached the screen: $message", message.contains("super-secret"))
        assertTrue(message.contains("api_key=<redacted>"))
    }
}
