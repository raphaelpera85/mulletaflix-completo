package org.mulletaflix.data.repository

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withContext
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * O identificador do aparelho é resolvido **uma vez**.
 *
 * `getDeviceId()` era chamado a cada requisição, e o primeiro boot dispara várias em
 * paralelo: todas liam `null` antes de qualquer escrita e cada uma gerava o seu
 * UUID. O DataStore serializa as gravações, então a última vencia e o mesmo
 * aparelho aparecia como dois dispositivos no servidor — com a sessão do login
 * possivelmente amarrada a um id que os relatórios seguintes não usam.
 *
 * O teste usa `Dispatchers.Default` de propósito: com o dispatcher de teste os
 * blocos rodam em sequência e a janela de corrida não existiria — o teste passaria
 * com o defeito no lugar.
 */
class SingleFlightIdTest {

    @Test
    fun `chamadas concorrentes recebem o mesmo identificador`() = runBlocking {
        var writes = 0
        var generated = 0
        val flight = SingleFlightId(
            read = {
                delay(1) // a leitura real suspende: é essa a janela
                null
            },
            write = { writes++ },
            newId = { "id-${++generated}" },
        )

        val ids = withContext(Dispatchers.Default) {
            (1..8).map { async { flight.get() } }.awaitAll()
        }

        assertEquals("todo mundo precisa ver o mesmo aparelho", 1, ids.distinct().size)
        assertEquals("só uma gravação pode acontecer", 1, writes)
        assertEquals("só um identificador pode ser gerado", 1, generated)
    }

    @Test
    fun `um identificador ja gravado e reaproveitado`() = runBlocking {
        var writes = 0
        var generated = 0
        val flight = SingleFlightId(
            read = { "id-guardado" },
            write = { writes++ },
            newId = { "id-${++generated}" },
        )

        val first = flight.get()
        val second = flight.get()

        assertEquals("id-guardado", first)
        assertEquals("id-guardado", second)
        assertEquals("não pode gerar sobre um valor existente", 0, generated)
        assertEquals("não pode gravar sobre um valor existente", 0, writes)
    }

    @Test
    fun `a segunda chamada nao volta a ler`() = runBlocking {
        var reads = 0
        val flight = SingleFlightId(
            read = { reads++; null },
            write = {},
            newId = { "id-fixo" },
        )

        flight.get()
        flight.get()
        flight.get()

        assertEquals("o valor resolvido fica em memória", 1, reads)
    }
}
