package org.mulletaflix.domain.repository

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * O `ErrorCode` que o servidor manda quando recusa preparar a mídia.
 *
 * O campo existia no DTO desde sempre e não tinha leitor nenhum: as três causas
 * abaixo chegavam à tela como a mesma frase. O valor de cada mensagem importa
 * menos que o fato de serem diferentes — é a diferença que diz ao usuário se ele
 * precisa de permissão, se precisa fechar outra reprodução, ou se precisa tentar
 * uma qualidade menor.
 */
class PlaybackPreparationFailureTest {

    @Test
    fun `cada motivo do servidor tem a sua propria frase`() {
        val messages = listOf(
            "NotAllowed",
            "RateLimitExceeded",
            "NoCompatibleStream",
            null,
        ).map(::playbackPreparationFailureMessage)

        assertEquals(
            "as quatro causas não podem colapsar na mesma frase",
            messages.size,
            messages.distinct().size,
        )
    }

    @Test
    fun `falta de permissao e limite de transmissoes não se confundem`() {
        val notAllowed = playbackPreparationFailureMessage("NotAllowed")
        val rateLimit = playbackPreparationFailureMessage("RateLimitExceeded")

        assertNotEquals(notAllowed, rateLimit)
        assertTrue(notAllowed.contains("permissão"))
        assertTrue(rateLimit.contains("simultâneas"))
    }

    @Test
    fun `um codigo desconhecido cai na frase generica`() {
        assertEquals(
            DEFAULT_PLAYBACK_PREPARATION_FAILURE,
            playbackPreparationFailureMessage("AlgoNovoDoServidor"),
        )
        assertEquals(
            DEFAULT_PLAYBACK_PREPARATION_FAILURE,
            playbackPreparationFailureMessage(null),
        )
    }

    @Test
    fun `a resposta sem motivo continua com a frase de sempre`() {
        val info = PlaybackInfo(playSessionId = "s", mediaSources = emptyList())
        assertNull(info.preparationError)
        assertEquals(DEFAULT_PLAYBACK_PREPARATION_FAILURE, info.unavailableMessage)
    }

    @Test
    fun `o motivo do servidor vence a frase generica`() {
        val info = PlaybackInfo(
            playSessionId = "s",
            mediaSources = emptyList(),
            preparationError = playbackPreparationFailureMessage("RateLimitExceeded"),
        )
        assertEquals(
            playbackPreparationFailureMessage("RateLimitExceeded"),
            info.unavailableMessage,
        )
        assertNotEquals(DEFAULT_PLAYBACK_PREPARATION_FAILURE, info.unavailableMessage)
    }
}
