package org.mulletaflix.android.update

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.mulletaflix.android.shouldShowAppUpdateDialog
import org.mulletaflix.core.common.update.AppUpdateDownloader
import org.mulletaflix.core.common.update.AppUpdateInstallOutcome
import org.mulletaflix.core.common.update.DownloadState
import org.mulletaflix.core.common.update.errorMessageOrNull
import org.mulletaflix.core.common.update.installDownloadedApk
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.usecase.CheckAppUpdateUseCase
import java.io.File
import javax.inject.Inject

/** Estado do aviso de atualização do app. */
data class AppUpdateState(
    val available: AppUpdateInfo? = null,
    val isDialogVisible: Boolean = false,
    val isDownloading: Boolean = false,
    val progress: Float = 0f,
    val error: String? = null,
)

/**
 * O aviso de atualização da `MainActivity`, com o estado fora da composição.
 *
 * **Por que isto saiu da Activity.** Os seis valores que controlavam o aviso
 * (`availableUpdate`, `isDownloadingUpdate`, `updateProgress`, `showUpdateDialog`,
 * `updateError`, `dismissedUpdateVersion`) eram `remember { mutableStateOf(...) }`, e o
 * download do APK rodava em `rememberCoroutineScope()`. Isso significa que **qualquer
 * recriação da Activity matava o download no meio**: o escopo de composição é cancelado
 * quando a composição sai, e `uiMode`, `density`, `fontScale` e `locale` **não** estão na
 * lista de `android:configChanges` do manifesto — então trocar o tema do sistema, o
 * tamanho da fonte, o tamanho da tela ou o idioma destruía a Activity. O download
 * morria, o `catch (CancellationException)` relançava, o estado voltava a "não estou
 * baixando" e o espectador via o mesmo diálogo de antes, sem nada dizendo que o download
 * tinha sido abortado. E a versão dispensada voltava a ser oferecida.
 *
 * Com o estado e o trabalho num `ViewModel` preso à Activity, a recriação não alcança
 * nem um nem outro: `viewModelScope` só é cancelado em `onCleared`, que acontece quando
 * a Activity é **encerrada**, não recriada. [dismissedVersion] é de propósito um campo
 * comum, e não algo persistido: "dispensado" vale para esta sessão, e a próxima abertura
 * do app pode voltar a oferecer a versão.
 */
@HiltViewModel
class AppUpdateViewModel @Inject constructor(
    private val checkAppUpdateUseCase: CheckAppUpdateUseCase,
    private val downloader: AppUpdateDownloader,
) : ViewModel() {

    private val _state = MutableStateFlow(AppUpdateState())
    val state: StateFlow<AppUpdateState> = _state.asStateFlow()

    /** Versão que o usuário mandou não mostrar de novo nesta sessão. */
    private var dismissedVersion: String? = null

    private var downloadJob: Job? = null
    private var checkJob: Job? = null

    /**
     * Consulta o GitHub e decide se há o que mostrar.
     *
     * Chamado a cada volta ao primeiro plano, como antes. Duas guardas são novas: não
     * consulta durante um download (o resultado não teria onde aparecer) e não deixa uma
     * resposta atrasada atropelar um download que começou depois dela.
     */
    fun checkForUpdate(currentVersion: String) {
        if (_state.value.isDownloading || checkJob?.isActive == true) return
        checkJob = viewModelScope.launch {
            runCatching { checkAppUpdateUseCase(currentVersion) }
                .getOrElse { Result.failure(it) }
                .onSuccess { info ->
                    _state.update { current ->
                        // A checagem é assíncrona: se um download começou enquanto ela
                        // corria, esta resposta é velha e não pode mexer no diálogo.
                        if (current.isDownloading) {
                            current
                        } else {
                            current.copy(
                                available = info,
                                isDialogVisible = shouldShowAppUpdateDialog(info, dismissedVersion),
                            )
                        }
                    }
                }
        }
    }

    /**
     * O usuário disse "Depois" (ou fechou o diálogo tocando fora).
     *
     * A versão fica marcada nesta sessão: um aviso que volta sozinho depois de ser
     * dispensado não é um aviso, é um incômodo.
     */
    fun dismissDialog() {
        _state.value.available?.latestVersion?.let { dismissedVersion = it }
        _state.update { it.copy(isDialogVisible = false) }
    }

    /**
     * Baixa o APK e abre o instalador.
     *
     * [install] entra como parâmetro em vez de ser chamado aqui dentro para o teste de
     * JVM exercitar o fluxo inteiro — sucesso, falha e instalação recusada — sem Android
     * e sem um `Context` dentro do `ViewModel`.
     */
    fun downloadUpdate(install: (File) -> Boolean) {
        val update = _state.value.available ?: return
        val url = update.apkDownloadUrl?.takeIf(String::isNotBlank) ?: return
        if (_state.value.isDownloading) return

        downloadJob = viewModelScope.launch {
            _state.update { it.copy(isDownloading = true, progress = 0f, error = null) }
            downloader.downloadApk(
                downloadUrl = url,
                versionName = update.latestVersion,
                expectedSha256 = update.apkSha256,
            ).collect { downloadState ->
                when (downloadState) {
                    is DownloadState.Downloading ->
                        _state.update { it.copy(progress = downloadState.progress) }

                    is DownloadState.Completed -> {
                        _state.update { it.copy(isDownloading = false) }
                        // A classificação do resultado (abriu, recusou, explodiu) e as
                        // mensagens estão em `installDownloadedApk`, compartilhadas com o
                        // Centro de Atualizações — aqui só se decide o que a tela mostra.
                        when (val outcome = installDownloadedApk(downloadState.file, install)) {
                            AppUpdateInstallOutcome.Started ->
                                _state.update { it.copy(isDialogVisible = false) }

                            else ->
                                _state.update { it.copy(error = outcome.errorMessageOrNull()) }
                        }
                    }

                    is DownloadState.Error ->
                        _state.update { it.copy(isDownloading = false, error = downloadState.message) }

                    DownloadState.Idle -> Unit
                }
            }
        }
    }
}
