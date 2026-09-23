package org.mulletaflix.feature.player

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Insistir na busca do próximo episódio, mas não para sempre.
 *
 * A busca rodava **uma vez**: se falhasse, o resultado era `null`, e `null` é
 * exatamente o que "esta série acabou" devolve. O player esconde o aviso de
 * "Próximo episódio" em `null`, então um erro passageiro de rede encerrava a
 * maratona sem nada na tela explicando a diferença.
 *
 * As duas políticas eram testadas desde a v1.2.79; **o laço que as usa** vivia dentro
 * do `PlayerViewModel` e só era provado por compilação. A partir da v1.2.83 ele é
 * `settleNextEpisodeLookup`, e os testes abaixo o alcançam — inclusive a espera entre
 * tentativas, que roda em tempo virtual do `runTest`.
 */
class NextEpisodeLookupRetryTest {

    @Test
    fun `insiste depois de uma falha, ate o limite`() {
        assertTrue("a primeira falha precisa de nova tentativa", shouldRetryNextEpisodeLookup(0))
        assertTrue("a segunda também", shouldRetryNextEpisodeLookup(1))
        assertFalse(
            "a terceira não: a busca é um extra, não pode ficar viva durante a reprodução inteira",
            shouldRetryNextEpisodeLookup(2),
        )
    }

    @Test
    fun `o numero de tentativas e o que o limite promete`() {
        val attempts = generateSequence(0) { it + 1 }
            .takeWhile { shouldRetryNextEpisodeLookup(it) }
            .count() + 1

        assertEquals(NEXT_EPISODE_LOOKUP_ATTEMPTS, attempts)
    }

    @Test
    fun `a espera cresce e tem teto`() {
        val delays = (0 until 4).map(::nextEpisodeLookupRetryDelayMs)

        assertEquals("crescente", delays.sorted(), delays)
        assertTrue("a primeira espera não pode ser instantânea", delays.first() > 0L)
        assertTrue("e nenhuma pode passar do teto", delays.all { it <= 8_000L })
    }

    @Test
    fun `um acerto de primeira nao e repetido`() = runTest {
        var calls = 0

        val result = settleNextEpisodeLookup {
            calls++
            Result.success("ep-2")
        }

        assertEquals("uma resposta boa encerra o assunto", 1, calls)
        assertEquals("ep-2", result.getOrNull())
    }

    @Test
    fun `fim de serie e resposta, nao falha, e nao se insiste nele`() = runTest {
        var calls = 0

        val result = settleNextEpisodeLookup<String> {
            calls++
            Result.success(null)
        }

        // `success(null)` significa "esta série não tem próximo episódio". Insistir
        // aqui transformaria o fim de uma série em três requisições inúteis — e o
        // teste existe para prender essa distinção.
        assertEquals("sucesso sem episódio não pode virar retentativa", 1, calls)
        assertTrue(result.isSuccess)
        assertNull(result.getOrNull())
    }

    @Test
    fun `duas falhas e um acerto devolvem o episodio`() = runTest {
        var calls = 0

        val result = settleNextEpisodeLookup {
            calls++
            if (calls < 3) Result.failure(IllegalStateException("HTTP 503")) else Result.success("ep-7")
        }

        assertEquals(3, calls)
        assertEquals("ep-7", result.getOrNull())
    }

    @Test
    fun `todas as falhas desistem na terceira e devolvem a ultima`() = runTest {
        var calls = 0
        val failure = IllegalStateException("servidor fora do ar")

        val result = settleNextEpisodeLookup<String> {
            calls++
            Result.failure(failure)
        }

        assertEquals("não pode tentar uma quarta vez", NEXT_EPISODE_LOOKUP_ATTEMPTS, calls)
        assertTrue("e precisa contar que falhou, não que a série acabou", result.isFailure)
        assertEquals(failure, result.exceptionOrNull())
    }

    @Test
    fun `a espera entre tentativas e a que a politica promete`() = runTest {
        // Sem esta asserção o laço poderia chamar `lookup` três vezes em sequência
        // imediata e todos os testes acima continuariam passando: o que se prova aqui
        // é que ele **espera**, e que a espera é a crescente da política.
        val expected = nextEpisodeLookupRetryDelayMs(0) + nextEpisodeLookupRetryDelayMs(1)

        settleNextEpisodeLookup<String> { Result.failure(IllegalStateException("HTTP 503")) }

        assertEquals("1000 + 2000 ms de espera acumulada", expected, testScheduler.currentTime)
    }
}
