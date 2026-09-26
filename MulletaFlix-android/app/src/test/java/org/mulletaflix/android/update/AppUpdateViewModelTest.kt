package org.mulletaflix.android.update

import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.core.common.update.AppUpdateDownloader
import org.mulletaflix.core.common.update.DownloadState
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.repository.AppUpdateRepository
import org.mulletaflix.domain.usecase.CheckAppUpdateUseCase
import java.io.File

/**
 * O aviso de atualização, com o estado fora da composição.
 *
 * Antes desta classe, o aviso vivia em `remember { mutableStateOf(...) }` dentro da
 * `MainActivity` e o download rodava em `rememberCoroutineScope()` — ou seja, **qualquer
 * recriação da Activity matava o download no meio** e o estado voltava ao início. Trocar
 * o tema do sistema, o tamanho da fonte, a densidade ou o idioma recria a Activity (nenhum
 * deles está em `android:configChanges`), então não era um caminho teórico.
 *
 * O que estes testes provam é o **comportamento** do fluxo (recusa, dispensa, progresso,
 * sucesso, falha, instalação recusada). A parte "sobrevive à recriação" é estrutural:
 * `viewModelScope` só é cancelado em `onCleared`, e o `ViewModel` pertence ao store da
 * Activity, que atravessa a recriação.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class AppUpdateViewModelTest {

    private val dispatcher = StandardTestDispatcher()

    @Before
    fun setUp() = Dispatchers.setMain(dispatcher)

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun update(
        version: String = "1.2.90",
        available: Boolean = true,
        downloadUrl: String? = "https://example.invalid/mulletaflix.apk",
    ) = AppUpdateInfo(
        isUpdateAvailable = available,
        currentVersion = "1.2.84",
        latestVersion = version,
        apkDownloadUrl = downloadUrl,
    )

    private class FakeCheckRepository(
        private val result: () -> Result<AppUpdateInfo>,
    ) : AppUpdateRepository {
        override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> = result()
    }

    /** Downloader de mentira: `AppUpdateDownloader` é final e leva um `Context`. */
    private fun downloaderEmitting(vararg states: DownloadState): AppUpdateDownloader =
        mockk<AppUpdateDownloader>().apply {
            every { downloadApk(any(), any(), any()) } returns flowOf(*states)
        }

    private fun viewModelReturning(
        info: AppUpdateInfo,
        downloader: AppUpdateDownloader = downloaderEmitting(),
    ) = AppUpdateViewModel(
        checkAppUpdateUseCase = CheckAppUpdateUseCase(FakeCheckRepository { Result.success(info) }),
        downloader = downloader,
    )

    @Test
    fun `overlapping update checks share one in-flight request`() = runTest {
        val response = CompletableDeferred<Result<AppUpdateInfo>>()
        var requests = 0
        val viewModel = AppUpdateViewModel(
            checkAppUpdateUseCase = CheckAppUpdateUseCase(object : AppUpdateRepository {
                override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                    requests++
                    return response.await()
                }
            }),
            downloader = downloaderEmitting(),
        )

        viewModel.checkForUpdate("1.2.84")
        runCurrent()
        viewModel.checkForUpdate("1.2.84")
        runCurrent()
        assertEquals(1, requests)

        response.complete(Result.success(update()))
        advanceUntilIdle()
        assertTrue(viewModel.state.value.isDialogVisible)
    }

    @Test
    fun `an available update is offered`() = runTest {
        val viewModel = viewModelReturning(update())

        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        assertEquals("1.2.90", viewModel.state.value.available?.latestVersion)
        assertTrue(viewModel.state.value.isDialogVisible)
    }

    @Test
    fun `a dismissed update is not offered again in the same session`() = runTest {
        val viewModel = viewModelReturning(update())

        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()
        viewModel.dismissDialog()
        assertFalse(viewModel.state.value.isDialogVisible)

        // A checagem roda a cada volta ao primeiro plano: sem a dispensa guardada, o
        // aviso voltaria sozinho segundos depois de o usuário dizer "Depois".
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        assertFalse("um aviso dispensado não pode voltar sozinho", viewModel.state.value.isDialogVisible)
    }

    @Test
    fun `a newer release than the dismissed one is offered again`() = runTest {
        var info = update(version = "1.2.90")
        val viewModel = AppUpdateViewModel(
            checkAppUpdateUseCase = CheckAppUpdateUseCase(FakeCheckRepository { Result.success(info) }),
            downloader = downloaderEmitting(),
        )

        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()
        viewModel.dismissDialog()

        info = update(version = "1.2.91")
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        // Dispensar a 1.2.90 não pode silenciar a 1.2.91: a dispensa é da versão, não do
        // recurso.
        assertTrue(viewModel.state.value.isDialogVisible)
        assertEquals("1.2.91", viewModel.state.value.available?.latestVersion)
    }

    @Test
    fun `an update without a download link is not offered`() = runTest {
        val viewModel = viewModelReturning(update(downloadUrl = null))

        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isDialogVisible)
    }

    @Test
    fun `a progress event reaches the state`() = runTest {
        val viewModel = viewModelReturning(
            info = update(),
            downloader = downloaderEmitting(DownloadState.Downloading(0.4f, 40L, 100L)),
        )
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        viewModel.downloadUpdate { true }
        advanceUntilIdle()

        assertEquals(0.4f, viewModel.state.value.progress, 0.001f)
    }

    @Test
    fun `a completed download asks for installation and hides the dialog`() = runTest {
        val installed = mutableListOf<File>()
        val viewModel = viewModelReturning(
            info = update(),
            downloader = downloaderEmitting(
                DownloadState.Downloading(0.5f, 50L, 100L),
                DownloadState.Completed(File("mulletaflix-app-v1.2.90.apk")),
            ),
        )
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        viewModel.downloadUpdate { file ->
            installed += file
            true
        }
        advanceUntilIdle()

        assertEquals(1, installed.size)
        assertFalse(viewModel.state.value.isDownloading)
        assertFalse("instalação aberta: o diálogo sai de cena", viewModel.state.value.isDialogVisible)
    }

    @Test
    fun `a refused installation explains what to allow`() = runTest {
        val viewModel = viewModelReturning(
            info = update(),
            downloader = downloaderEmitting(DownloadState.Completed(File("mulletaflix-app-v1.2.90.apk"))),
        )
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        viewModel.downloadUpdate { false }
        advanceUntilIdle()

        // O diálogo continua (a instalação não abriu) e a tela diz o que fazer.
        assertTrue(viewModel.state.value.isDialogVisible)
        assertEquals(
            "Permita a instalação de fontes desconhecidas e tente novamente.",
            viewModel.state.value.error,
        )
    }

    @Test
    fun `a download error stops the busy state and keeps the message`() = runTest {
        val viewModel = viewModelReturning(
            info = update(),
            downloader = downloaderEmitting(
                DownloadState.Downloading(0.1f, 10L, 100L),
                DownloadState.Error("Falha no download do APK: HTTP 503"),
            ),
        )
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        viewModel.downloadUpdate { true }
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isDownloading)
        assertEquals("Falha no download do APK: HTTP 503", viewModel.state.value.error)
        assertTrue("o botão vira 'Tentar novamente'", viewModel.state.value.isDialogVisible)
    }

    @Test
    fun `a check that lands mid-download cannot close the dialog`() = runTest {
        // A checagem roda a cada volta ao primeiro plano e é assíncrona: a resposta pode
        // chegar depois de o download ter começado. Se ela mexesse no diálogo, o
        // espectador veria a barra de progresso desaparecer com o download ainda
        // rodando — foi por isso que o teste anterior (que só conferia `progress`) não
        // servia: `copy()` preserva o progresso mesmo sem guarda nenhuma.
        var info: AppUpdateInfo? = update()
        val viewModel = AppUpdateViewModel(
            checkAppUpdateUseCase = CheckAppUpdateUseCase(
                FakeCheckRepository {
                    info?.let { Result.success(it) }
                        ?: Result.success(update(available = false, downloadUrl = null))
                },
            ),
            downloader = downloaderEmitting(DownloadState.Downloading(0.3f, 30L, 100L)),
        )
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        viewModel.downloadUpdate { true }
        advanceUntilIdle()
        assertTrue(viewModel.state.value.isDownloading)

        // A partir daqui o servidor "não tem mais novidade" — uma resposta velha.
        info = null
        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        assertTrue("o download continua e precisa continuar visível", viewModel.state.value.isDownloading)
        assertTrue("uma resposta velha não pode fechar o diálogo", viewModel.state.value.isDialogVisible)
        assertEquals(0.3f, viewModel.state.value.progress, 0.001f)
    }

    @Test
    fun `a failed update check leaves the screen alone`() = runTest {
        val viewModel = AppUpdateViewModel(
            checkAppUpdateUseCase = CheckAppUpdateUseCase(
                FakeCheckRepository { Result.failure(IllegalStateException("rede fora")) },
            ),
            downloader = downloaderEmitting(),
        )

        viewModel.checkForUpdate("1.2.84")
        advanceUntilIdle()

        // Verificação de atualização é um extra e não bloqueia nada: uma falha não vira
        // diálogo nem mensagem.
        assertFalse(viewModel.state.value.isDialogVisible)
        assertEquals(null, viewModel.state.value.error)
    }
}
