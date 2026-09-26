package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * "Tentar novamente" tem quatro respostas possíveis e a escolha entre elas já
 * produziu dois defeitos reais:
 *
 *  - durante uma queda de rede o botão voltava em silêncio, e o usuário
 *    concluía que estava quebrado (v1.2.55);
 *  - ao repintar a mídia, a posição voltava para o começo do filme se a
 *    posição do erro não fosse considerada.
 *
 * A decisão estava em quatro `if` dentro de um `ViewModel` que constrói o
 * próprio `ExoPlayer`, então nenhum teste podia alcançá-la.
 */
class PlaybackRetryPlanTest {

    private fun plan(
        itemId: String? = "item-1",
        offlinePlayback: Boolean = false,
        networkOffline: Boolean = false,
        preparedMedia: Boolean = true,
        currentPositionMs: Long = 0L,
        positionAtErrorMs: Long = 0L,
    ) = playbackRetryPlan(
        itemId = itemId,
        isOfflinePlayback = offlinePlayback,
        isNetworkOffline = networkOffline,
        hasPreparedMedia = preparedMedia,
        currentPositionMs = currentPositionMs,
        positionAtErrorMs = positionAtErrorMs,
    )

    @Test
    fun `sem item carregado nao ha o que tentar de novo`() {
        assertEquals(PlaybackRetryPlan.Nothing, plan(itemId = null))
    }

    @Test
    fun `uma queda de rede avisa em vez de nao fazer nada`() {
        // O defeito da v1.2.55: retornar em silêncio fazia o botão parecer quebrado.
        val decided = plan(networkOffline = true, preparedMedia = true)
        assertEquals(PlaybackRetryPlan.WarnOffline, decided)
        assertNotEquals(
            "retornar em silêncio é o defeito, não o comportamento",
            PlaybackRetryPlan.Nothing,
            decided,
        )
    }

    @Test
    fun `a queda de rede vence a midia ja preparada`() {
        // Repreparar durante a queda troca uma falha por outra: o aviso vem primeiro.
        assertEquals(
            PlaybackRetryPlan.WarnOffline,
            plan(networkOffline = true, preparedMedia = true),
        )
    }

    @Test
    fun `sem midia preparada o item e buscado de novo`() {
        assertEquals(PlaybackRetryPlan.Reload, plan(preparedMedia = false))
    }

    @Test
    fun `um arquivo baixado nao tenta nada, com ou sem rede`() {
        // Um download não depende do servidor nem da rede, então não há o que
        // reiniciar — e recarregar pelo itemId apontaria o player para a rede.
        assertEquals(PlaybackRetryPlan.Nothing, plan(offlinePlayback = true))
        assertEquals(
            PlaybackRetryPlan.Nothing,
            plan(offlinePlayback = true, networkOffline = true),
        )
        assertEquals(
            "offline vence até a falta de mídia preparada",
            PlaybackRetryPlan.Nothing,
            plan(offlinePlayback = true, preparedMedia = false),
        )
    }

    @Test
    fun `midia preparada reinicia na posicao mais adiantada que se conhece`() {
        // O defeito que a v1.2.55 também corrigiu: voltar para o começo do filme.
        assertEquals(
            PlaybackRetryPlan.Restart(90_000L),
            plan(currentPositionMs = 30_000L, positionAtErrorMs = 90_000L),
        )
        assertEquals(
            PlaybackRetryPlan.Restart(90_000L),
            plan(currentPositionMs = 90_000L, positionAtErrorMs = 0L),
        )
    }

    @Test
    fun `a posicao de reinicio nunca e negativa`() {
        val decided = plan(currentPositionMs = -1L, positionAtErrorMs = -5_000L)
        assertEquals(PlaybackRetryPlan.Restart(0L), decided)
        assertTrue(
            "uma posição negativa no seek é um salto para o fim do arquivo",
            (decided as PlaybackRetryPlan.Restart).positionMs >= 0L,
        )
    }

    @Test
    fun `um item ausente vence todos os outros motivos`() {
        // A ordem das perguntas é a ordem das consequências: sem item não existe
        // nem aviso, nem recarga, nem reinício.
        assertEquals(
            PlaybackRetryPlan.Nothing,
            plan(itemId = null, networkOffline = true, preparedMedia = false),
        )
    }
}
