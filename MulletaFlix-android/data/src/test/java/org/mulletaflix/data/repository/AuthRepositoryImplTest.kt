package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.QuickConnectResultDto
import org.mulletaflix.core.api.dto.PublicSystemInfoDto

/**
 * A sessão precisa acompanhar o servidor a que pertence.
 *
 * `verifyServer` reescreve o endereço antes de a verificação terminar. Se o
 * servidor verificado for outro, o token e o usuário guardados continuam sendo do
 * servidor anterior — e a partir dali o app acredita estar autenticado e manda
 * essa credencial para o host novo.
 */
class AuthRepositoryImplTest {

    private val api = mockk<MulletaFlixApiService>()
    private val sessionRepository = mockk<SessionRepository>(relaxUnitFun = true)

    private fun repository() = AuthRepositoryImpl(api, sessionRepository)

    private fun stubServer(
        url: String = "http://server-a:8096",
        serverId: String? = "server-a",
    ) {
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow(url)
        every { sessionRepository.getServerId() } returns MutableStateFlow(serverId)
        coEvery { api.getPublicSystemInfo() } returns PublicSystemInfoDto(
            serverName = "MulletaFlix",
            version = "12.0.27",
            id = "server-b",
        )
    }

    @Test
    fun `apontar para outro servidor derruba a sessao do servidor anterior`() = runBlocking {
        stubServer(url = "http://server-a:8096", serverId = "server-a")

        val result = repository().verifyServer("http://server-b:8096")

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { sessionRepository.setBaseUrl("http://server-b:8096") }
        coVerify(exactly = 1) { sessionRepository.clearSession() }
    }

    @Test
    fun `o mesmo servidor em outro endereco nao derruba nada`() = runBlocking {
        // LAN e DuckDNS do mesmo servidor: a troca que o app faz sozinho quando o
        // Wi-Fi cai não pode deslogar o usuário.
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow("http://192.168.15.9:8096")
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { api.getPublicSystemInfo() } returns PublicSystemInfoDto(
            serverName = "MulletaFlix",
            version = "12.0.27",
            id = "server-a",
        )

        val result = repository().verifyServer("http://mulletaflix.duckdns.org:8096")

        assertTrue(result.isSuccess)
        coVerify(exactly = 0) { sessionRepository.clearSession() }
    }

    @Test
    fun `sem identidade de um dos lados a sessao e mantida`() = runBlocking {
        // Não dá para provar que o servidor mudou; deslogar por isso seria pior.
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow("http://server-a:8096")
        every { sessionRepository.getServerId() } returns MutableStateFlow(null)
        coEvery { api.getPublicSystemInfo() } returns PublicSystemInfoDto(
            serverName = "MulletaFlix",
            version = "12.0.27",
            id = "server-b",
        )

        assertTrue(repository().verifyServer("http://server-b:8096").isSuccess)

        coVerify(exactly = 0) { sessionRepository.clearSession() }
    }

    @Test
    fun `uma verificacao que falha devolve o endereco anterior`() = runBlocking {
        val baseUrl = MutableStateFlow("http://192.168.15.9:8096")
        every { sessionRepository.getBaseUrl() } returns baseUrl
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { sessionRepository.setBaseUrl(any()) } answers {
            baseUrl.value = firstArg()
        }
        coEvery { api.getPublicSystemInfo() } throws IllegalStateException("sem rede")

        val result = repository().verifyServer("http://servidor-morto:8096")

        assertTrue(result.isFailure)
        assertEquals(
            "o endereço de um servidor que não respondeu não pode ficar gravado",
            "http://192.168.15.9:8096",
            baseUrl.value,
        )
    }

    /**
     * Cancelamento não é falha de rede.
     *
     * `runCatching` pegava `Throwable`, então um `loadJob?.cancel()` — que este app
     * usa em toda tela ao recarregar — virava `Result.failure` e a tela escrevia um
     * erro para um trabalho abandonado. Na prática, o pior efeito era aqui: a
     * restauração do endereço é `suspend` e não rodava numa corrotina cancelada.
     */
    @Test
    fun `cancelamento nao vira falha`() = runBlocking {
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow("http://server-a:8096")
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { api.getPublicSystemInfo() } throws kotlinx.coroutines.CancellationException("cancelado")

        val thrown = runCatching { repository().verifyServer("http://server-b:8096") }

        assertTrue(
            "cancelamento precisa subir, não virar Result.failure",
            thrown.exceptionOrNull() is kotlinx.coroutines.CancellationException,
        )
    }

    /**
     * A restauração do endereço precisa acontecer **mesmo** com a corrotina cancelada.
     *
     * `AuthViewModel.connectToServer` cancela a tentativa anterior (`connectionJob?.cancel()`)
     * quando o usuário escolhe outro servidor. `setBaseUrl` é `suspend` e grava no
     * DataStore — um ponto de suspensão de verdade —, então numa corrotina já
     * cancelada ele lança antes de escrever e o endereço do servidor morto ficava
     * gravado. O `withContext(NonCancellable)` existe para isso, e este teste usa um
     * `setBaseUrl` que realmente suspende (um mock que só atribui uma variável não
     * teria o ponto de suspensão e o teste passaria com o defeito no lugar).
     */
    @Test
    fun `a restauracao do endereco acontece mesmo com a corrotina cancelada`() = runBlocking {
        val previous = "http://192.168.15.9:8096"
        val baseUrl = MutableStateFlow(previous)
        val firstWriteDone = CompletableDeferred<Unit>()
        every { sessionRepository.getBaseUrl() } returns baseUrl
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { sessionRepository.setBaseUrl(any()) } coAnswers {
            delay(1)
            baseUrl.value = firstArg()
            if (!firstWriteDone.isCompleted) firstWriteDone.complete(Unit)
        }
        coEvery { api.getPublicSystemInfo() } coAnswers {
            CompletableDeferred<Unit>().await()
            PublicSystemInfoDto()
        }

        val attempt = launch {
            runCatching { repository().verifyServer("http://servidor-morto:8096") }
        }
        firstWriteDone.await()
        assertEquals("http://servidor-morto:8096", baseUrl.value)

        attempt.cancelAndJoin()

        assertEquals(
            "o endereço de um servidor que não respondeu não pode ficar gravado",
            previous,
            baseUrl.value,
        )
    }

    @Test
    fun `o dto do servidor verificado vira o ServerVerification`() = runBlocking {
        stubServer(serverId = "server-b")

        val verification = repository().verifyServer("http://server-b:8096/").getOrThrow()

        assertEquals("MulletaFlix", verification.name)
        assertEquals("12.0.27", verification.version)
        assertEquals("server-b", verification.serverId)
        assertTrue((verification.latencyMs ?: -1L) >= 0L)
    }

    @Test
    fun `quick connect normaliza o codigo e segredo recebidos`() = runBlocking {
        coEvery { api.initiateQuickConnect() } returns QuickConnectResultDto(
            code = " 393877 ",
            secret = " secret-1 ",
        )

        val quickConnect = repository().initiateQuickConnect().getOrThrow()

        assertEquals("393877", quickConnect.code)
        assertEquals("secret-1", quickConnect.secret)
    }

    @Test
    fun `quick connect rejeita resposta sem codigo ou segredo`() = runBlocking {
        coEvery { api.initiateQuickConnect() } returns QuickConnectResultDto(
            code = " ",
            secret = "secret-1",
        )

        val result = repository().initiateQuickConnect()

        assertTrue(result.isFailure)
        assertEquals(
            "O servidor retornou um código Quick Connect inválido",
            result.exceptionOrNull()?.message,
        )
    }
}
