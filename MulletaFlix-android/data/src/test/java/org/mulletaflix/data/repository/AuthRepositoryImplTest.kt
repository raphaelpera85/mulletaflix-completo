package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.coVerifyOrder
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.PublicServerVerificationRequest
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.QuickConnectResultDto
import org.mulletaflix.core.api.dto.PublicSystemInfoDto

/**
 * A sessão precisa acompanhar o servidor a que pertence.
 *
 * `verifyServer` consulta o endereço candidato sem mudar o endpoint compartilhado
 * nem enviar o token salvo. Só aplica o endereço depois de identificar o servidor.
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
        coEvery { api.getPublicSystemInfo(any()) } returns PublicSystemInfoDto(
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
        coVerifyOrder {
            api.getPublicSystemInfo(PublicServerVerificationRequest("http://server-b:8096"))
            sessionRepository.clearSession()
            sessionRepository.setBaseUrl("http://server-b:8096")
        }
    }

    @Test
    fun `o mesmo servidor em outro endereco nao derruba nada`() = runBlocking {
        // LAN e DuckDNS do mesmo servidor: a troca que o app faz sozinho quando o
        // Wi-Fi cai não pode deslogar o usuário.
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow("http://192.168.15.9:8096")
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { api.getPublicSystemInfo(any()) } returns PublicSystemInfoDto(
            serverName = "MulletaFlix",
            version = "12.0.27",
            id = "server-a",
        )

        val result = repository().verifyServer("http://mulletaflix.duckdns.org:8096")

        assertTrue(result.isSuccess)
        coVerify(exactly = 0) { sessionRepository.clearSession() }
        coVerify(exactly = 1) { sessionRepository.setBaseUrl("http://mulletaflix.duckdns.org:8096") }
        coVerify(exactly = 1) {
            api.getPublicSystemInfo(PublicServerVerificationRequest("http://mulletaflix.duckdns.org:8096"))
        }
    }

    @Test
    fun `sem identidade guardada descarta sessao mesmo se resposta identifica servidor`() = runBlocking {
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow("http://server-a:8096")
        every { sessionRepository.getServerId() } returns MutableStateFlow(null)
        coEvery { api.getPublicSystemInfo(any()) } returns PublicSystemInfoDto(
            serverName = "MulletaFlix",
            version = "12.0.27",
            id = "server-b",
        )

        assertTrue(repository().verifyServer("http://server-b:8096").isSuccess)

        coVerify(exactly = 1) { sessionRepository.clearSession() }
    }

    @Test
    fun `resposta sem identidade descarta sessao mesmo no mesmo endpoint`() = runBlocking {
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow("http://server-a:8096")
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { api.getPublicSystemInfo(any()) } returns PublicSystemInfoDto(
            serverName = "MulletaFlix",
            version = "12.0.27",
            id = null,
        )

        assertTrue(repository().verifyServer("http://server-a:8096").isSuccess)

        coVerify(exactly = 1) { sessionRepository.clearSession() }
    }

    @Test
    fun `uma verificacao que falha devolve o endereco anterior`() = runBlocking {
        val baseUrl = MutableStateFlow("http://192.168.15.9:8096")
        every { sessionRepository.getBaseUrl() } returns baseUrl
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { api.getPublicSystemInfo(any()) } throws IllegalStateException("sem rede")

        val result = repository().verifyServer("http://servidor-morto:8096")

        assertTrue(result.isFailure)
        assertEquals(
            "uma verificação malsucedida não altera o endereço salvo",
            "http://192.168.15.9:8096",
            baseUrl.value,
        )
        coVerify(exactly = 0) { sessionRepository.setBaseUrl(any()) }
    }

    /** Cancellation remains distinct from a failed server response. */
    @Test
    fun `cancelamento nao vira falha`() = runBlocking {
        every { sessionRepository.getBaseUrl() } returns MutableStateFlow("http://server-a:8096")
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { api.getPublicSystemInfo(any()) } throws kotlinx.coroutines.CancellationException("cancelado")

        val thrown = runCatching { repository().verifyServer("http://server-b:8096") }

        assertTrue(
            "cancelamento precisa subir, não virar Result.failure",
            thrown.exceptionOrNull() is kotlinx.coroutines.CancellationException,
        )
    }

    @Test
    fun `cancelar verificacao nao modifica endpoint ou sessao`() = runBlocking {
        val previous = "http://192.168.15.9:8096"
        val baseUrl = MutableStateFlow(previous)
        val requestStarted = CompletableDeferred<Unit>()
        every { sessionRepository.getBaseUrl() } returns baseUrl
        every { sessionRepository.getServerId() } returns MutableStateFlow("server-a")
        coEvery { api.getPublicSystemInfo(any()) } coAnswers {
            requestStarted.complete(Unit)
            CompletableDeferred<Unit>().await()
            PublicSystemInfoDto()
        }

        val attempt = launch {
            runCatching { repository().verifyServer("http://servidor-morto:8096") }
        }
        requestStarted.await()
        attempt.cancelAndJoin()

        assertEquals(previous, baseUrl.value)
        coVerify(exactly = 0) { sessionRepository.setBaseUrl(any()) }
        coVerify(exactly = 0) { sessionRepository.clearSession() }
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
