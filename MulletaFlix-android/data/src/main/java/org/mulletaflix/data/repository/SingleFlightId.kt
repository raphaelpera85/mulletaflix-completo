package org.mulletaflix.data.repository

import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * Resolve um identificador **uma vez**, mesmo com chamadas concorrentes.
 *
 * O `DeviceId` vivia num ler-gerar-gravar sem trava, e o interceptor de identidade
 * chama `getDeviceId().first()` **a cada requisição**. No primeiro boot a Home
 * dispara várias requisições ao mesmo tempo: todas liam `null` antes de qualquer
 * escrita e cada uma criava o seu próprio UUID. O DataStore serializa as gravações,
 * então a última vencia — e o mesmo aparelho passava a aparecer como **dois
 * dispositivos** no servidor, com a sessão criada no login possivelmente amarrada a
 * um id diferente do que os relatórios seguintes usam.
 *
 * A primeira leitura é a única que pode gerar; depois disso o valor resolvido fica
 * em memória e a resposta é imediata.
 */
internal class SingleFlightId(
    private val read: suspend () -> String?,
    private val write: suspend (String) -> Unit,
    private val newId: () -> String,
) {
    private val mutex = Mutex()

    @Volatile
    private var resolved: String? = null

    suspend fun get(): String {
        resolved?.let { return it }
        return mutex.withLock {
            // A segunda checagem é o que faz a diferença: quem entrou na fila
            // enquanto o primeiro gerava encontra o valor pronto em vez de gerar
            // outro.
            resolved?.let { return@withLock it }
            val stored = read()
            val id = stored ?: newId().also { write(it) }
            resolved = id
            id
        }
    }
}
