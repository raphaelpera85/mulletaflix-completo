package org.mulletaflix.feature.player

import kotlinx.coroutines.delay

/** Quantas vezes a busca do próximo episódio é tentada antes de desistir. */
internal const val NEXT_EPISODE_LOOKUP_ATTEMPTS = 3

/** Espera entre tentativas, em milissegundos, crescente e limitada. */
internal fun nextEpisodeLookupRetryDelayMs(attempt: Int): Long =
    (1_000L * (attempt + 1)).coerceAtMost(8_000L)

/**
 * Insistir na busca do próximo episódio depois de uma falha.
 *
 * A busca rodava uma vez e, se falhasse, o resultado era `null` — o mesmo que "esta
 * série acabou". O player esconde o aviso de "Próximo episódio" em `null`, então um
 * 5xx passageiro encerrava a maratona em silêncio, sem nada na tela que explicasse a
 * diferença. `GetNextEpisodeUseCase` passou a propagar a falha; isto é o que o
 * chamador faz com ela.
 *
 * O teto é curto de propósito: a busca é um extra, e três tentativas cobrem a janela
 * de um erro transitório sem manter trabalho vivo durante a reprodução inteira.
 */
internal fun shouldRetryNextEpisodeLookup(attempt: Int): Boolean =
    attempt + 1 < NEXT_EPISODE_LOOKUP_ATTEMPTS

/**
 * Insiste em [lookup] até obter uma resposta conclusiva e devolve a que valeu.
 *
 * Este laço vivia solto dentro do `PlayerViewModel`, onde nenhum teste alcança: as duas
 * políticas acima eram testadas e o que o laço fazia **com** elas — quantas vezes
 * chamar, esperar quanto, quando parar de insistir — ficava provado só por compilação.
 * Extraído, ele roda em teste de JVM com tempo virtual, e a espera entre tentativas
 * deixa de ser invisível.
 *
 * Duas coisas contam como resposta e encerram o laço:
 *  - **sucesso**, com ou sem episódio. `success(null)` significa "esta série não tem
 *    próximo episódio" — é uma resposta, não uma falha, e insistir nela seria
 *    transformar o fim de uma série em três requisições inúteis;
 *  - **teto de tentativas**, quando todas falharam. Aí a última falha é devolvida, e o
 *    chamador decide o que dizer.
 *
 * Só a falha faz esperar e tentar de novo.
 */
internal suspend fun <T> settleNextEpisodeLookup(
    lookup: suspend () -> Result<T?>,
): Result<T?> {
    var attempt = 0
    while (true) {
        val result = lookup()
        if (result.isSuccess || !shouldRetryNextEpisodeLookup(attempt)) return result
        delay(nextEpisodeLookupRetryDelayMs(attempt))
        attempt++
    }
}
