package org.mulletaflix.feature.settings

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.coroutines.launch
import org.mulletaflix.core.common.dispatcher.IoDispatcher
import org.mulletaflix.core.common.update.AppUpdateDownloader
import org.mulletaflix.core.common.update.AppUpdateInstallOutcome
import org.mulletaflix.core.common.update.DownloadState
import org.mulletaflix.core.common.update.errorMessageOrNull
import org.mulletaflix.core.common.update.installDownloadedApk
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.model.LibrarySortField
import org.mulletaflix.domain.model.MediaLanguage
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.SearchHistoryRepository
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.model.normalizeSubtitleSizePercent
import org.mulletaflix.domain.usecase.CheckAppUpdateUseCase
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.VerifyServerUseCase
import javax.inject.Inject

data class SettingsState(
    val serverUrl: String? = null,
    val username: String? = null,
    val theme: MulletaFlixThemeVariant = MulletaFlixThemeVariant.Dark,
    val defaultQuality: String = "Auto",
    val aspectRatio: String = DEFAULT_ASPECT_RATIO,
    val defaultSpeed: Float = 1.0f,
    val autoPlay: Boolean = true,
    val skipIntro: Boolean = true,
    val pictureInPicture: Boolean = true,
    val audioLanguage: String = "Português (Brasil)",
    val subtitleLanguage: String = "Português (Brasil)",
    val subtitleFontSize: Int = 100,
    val subtitleColor: String = "Branco",
    val libraryGridDensity: String = "Confortável",
    val librarySort: String = "Nome",
    val librarySortOrder: String = "Ascendente",
    val downloadPath: String = "Armazenamento Interno",
    val downloadStorageGb: Int = 0,
    val isCheckingUpdate: Boolean = false,
    val updateInfo: AppUpdateInfo? = null,
    val isDownloadingUpdate: Boolean = false,
    val updateDownloadProgress: Float = 0f,
    val updateStatusMessage: String? = null,
    val updateErrorMessage: String? = null,
    val showUpdateDialog: Boolean = false,
    val isCheckingConnection: Boolean = false,
    val connectionStatus: String? = null,
    val cacheStatusMessage: String? = null,
)

@HiltViewModel
class SettingsViewModel @Inject constructor(
    @param:ApplicationContext private val context: Context,
    private val settingsRepository: SettingsRepository,
    private val authRepository: AuthRepository,
    private val logoutUseCase: LogoutUseCase,
    private val checkAppUpdateUseCase: CheckAppUpdateUseCase? = null,
    private val appUpdateDownloader: AppUpdateDownloader? = null,
    private val verifyServerUseCase: VerifyServerUseCase? = null,
    private val searchHistoryRepository: SearchHistoryRepository? = null,
    @param:IoDispatcher private val ioDispatcher: CoroutineDispatcher = Dispatchers.IO,
) : ViewModel() {

    private val _state = MutableStateFlow(SettingsState())
    val state: StateFlow<SettingsState> = _state.asStateFlow()

    init {
        refreshStorageInfo()
        viewModelScope.launch {
            authRepository.getSavedServerUrl().collect { url ->
                _state.update { it.copy(serverUrl = url.ifBlank { null }) }
            }
        }
        viewModelScope.launch {
            combine(
                authRepository.getSavedUserName(),
                authRepository.getSavedUserId(),
            ) { name, id ->
                when {
                    !name.isNullOrBlank() -> name
                    !id.isNullOrBlank() -> "Usuário Ativo"
                    else -> null
                }
            }.collect { resolvedUsername ->
                _state.update { it.copy(username = resolvedUsername) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getTheme().collect { domainTheme ->
                // Shared with the app root, which owns the actual theming. A
                // private copy here is how the two drifted apart before.
                _state.update { it.copy(theme = domainTheme.toThemeVariant()) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getPreferredAudioLanguage().collect { language ->
                _state.update { it.copy(audioLanguage = MediaLanguage.label(language)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getPreferredSubtitleLanguage().collect { language ->
                _state.update { it.copy(subtitleLanguage = MediaLanguage.label(language)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isAutoPlayEnabled().collect { enabled ->
                _state.update { it.copy(autoPlay = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isSkipIntroEnabled().collect { enabled ->
                _state.update { it.copy(skipIntro = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.isPiPEnabled().collect { enabled ->
                _state.update { it.copy(pictureInPicture = enabled) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultQuality().collect { quality ->
                _state.update { it.copy(defaultQuality = normalizeDefaultQuality(quality)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultAspectRatio().collect { ratio ->
                _state.update { it.copy(aspectRatio = normalizeAspectRatioPreferenceName(ratio)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultPlaybackSpeed().collect { speed ->
                _state.update { it.copy(defaultSpeed = speed) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getSubtitleFontSize().collect { size ->
                _state.update { it.copy(subtitleFontSize = normalizeSubtitleSizePercent(size)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getSubtitleColor().collect { color ->
                _state.update { it.copy(subtitleColor = subtitleColorLabel(color)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getLibraryGridDensity().collect { density ->
                _state.update { it.copy(libraryGridDensity = libraryGridDensityLabel(density)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultLibrarySort().collect { sortBy ->
                _state.update { it.copy(librarySort = librarySortLabel(sortBy)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getDefaultLibrarySortOrder().collect { sortOrder ->
                _state.update { it.copy(librarySortOrder = librarySortOrderLabel(sortOrder)) }
            }
        }
    }

    fun refreshStorageInfo() {
        viewModelScope.launch {
            val usableBytes = withContext(ioDispatcher) { context.cacheDir.usableSpace }
            _state.update { it.copy(downloadStorageGb = availableStorageGb(usableBytes)) }
        }
    }

    fun setTheme(theme: MulletaFlixThemeVariant) {
        _state.update { it.copy(theme = theme) }
        viewModelScope.launch {
            settingsRepository.setTheme(theme.toAppThemeSetting())
        }
    }

    fun setAutoPlay(autoPlay: Boolean) {
        _state.update { it.copy(autoPlay = autoPlay) }
        viewModelScope.launch { settingsRepository.setAutoPlayEnabled(autoPlay) }
    }

    fun setSubtitleLanguage(language: String) {
        _state.update { it.copy(subtitleLanguage = MediaLanguage.label(language)) }
        viewModelScope.launch {
            settingsRepository.setPreferredSubtitleLanguage(MediaLanguage.code(language))
        }
    }

    fun setAudioLanguage(language: String) {
        _state.update { it.copy(audioLanguage = MediaLanguage.label(language)) }
        viewModelScope.launch {
            settingsRepository.setPreferredAudioLanguage(MediaLanguage.code(language))
        }
    }

    fun setSkipIntro(skipIntro: Boolean) {
        _state.update { it.copy(skipIntro = skipIntro) }
        viewModelScope.launch { settingsRepository.setSkipIntroEnabled(skipIntro) }
    }

    fun setPictureInPicture(enabled: Boolean) {
        _state.update { it.copy(pictureInPicture = enabled) }
        viewModelScope.launch { settingsRepository.setPiPEnabled(enabled) }
    }

    fun setDefaultQuality(quality: String) {
        val normalized = normalizeDefaultQuality(quality)
        _state.update { it.copy(defaultQuality = normalized) }
        viewModelScope.launch { settingsRepository.setDefaultQuality(normalized) }
    }

    fun setDefaultAspectRatio(aspectRatio: String) {
        val normalized = normalizeAspectRatioPreferenceName(aspectRatio)
        _state.update { it.copy(aspectRatio = normalized) }
        viewModelScope.launch { settingsRepository.setDefaultAspectRatio(normalized) }
    }

    fun setDefaultPlaybackSpeed(speed: Float) {
        val normalized = speed.coerceIn(0.5f, 2f)
        _state.update { it.copy(defaultSpeed = normalized) }
        viewModelScope.launch { settingsRepository.setDefaultPlaybackSpeed(normalized) }
    }

    fun setSubtitleFontSize(size: Int) {
        val normalized = normalizeSubtitleSizePercent(size)
        _state.update { it.copy(subtitleFontSize = normalized) }
        viewModelScope.launch { settingsRepository.setSubtitleFontSize(normalized) }
    }

    fun setSubtitleColor(color: String) {
        val normalized = subtitleColorCode(color)
        _state.update { it.copy(subtitleColor = subtitleColorLabel(normalized)) }
        viewModelScope.launch { settingsRepository.setSubtitleColor(normalized) }
    }

    fun setLibraryGridDensity(density: String) {
        val normalized = libraryGridDensityCode(density)
        _state.update { it.copy(libraryGridDensity = libraryGridDensityLabel(normalized)) }
        viewModelScope.launch { settingsRepository.setLibraryGridDensity(normalized) }
    }

    fun setLibrarySort(label: String) {
        val normalized = librarySortCode(label)
        _state.update { it.copy(librarySort = librarySortLabel(normalized)) }
        viewModelScope.launch { settingsRepository.setDefaultLibrarySort(normalized) }
    }

    fun setLibrarySortOrder(label: String) {
        val normalized = librarySortOrderCode(label)
        _state.update { it.copy(librarySortOrder = librarySortOrderLabel(normalized)) }
        viewModelScope.launch { settingsRepository.setDefaultLibrarySortOrder(normalized) }
    }

    fun logout() {
        viewModelScope.launch {
            logoutUseCase()
        }
    }

    fun checkServerConnection() {
        val url = _state.value.serverUrl?.trim().orEmpty()
        val verifier = verifyServerUseCase
        if (url.isBlank() || verifier == null) {
            _state.update { it.copy(connectionStatus = "Servidor não configurado.") }
            return
        }
        viewModelScope.launch {
            _state.update { it.copy(isCheckingConnection = true, connectionStatus = null) }
            verifier(url)
                .onSuccess { verification ->
                    val latency = verification.latencyMs?.let { " • ${it} ms" }.orEmpty()
                    val version = verification.version?.let { " • v$it" }.orEmpty()
                    _state.update {
                        it.copy(
                            isCheckingConnection = false,
                            connectionStatus = "Conectado$latency$version",
                        )
                    }
                }
                .onFailure { error ->
                    _state.update {
                        it.copy(
                            isCheckingConnection = false,
                            connectionStatus = error.localizedMessage
                                ?.takeIf(String::isNotBlank)
                                ?: "Não foi possível conectar ao servidor.",
                        )
                    }
                }
        }
    }

    fun clearImageCache() {
        viewModelScope.launch {
            runCatching {
                withContext(ioDispatcher) {
                    context.cacheDir.resolve("image_cache").deleteRecursively()
                    context.cacheDir.resolve("coil").deleteRecursively()
                }
            }.onSuccess {
                _state.update { it.copy(cacheStatusMessage = "Cache de imagens limpo.") }
            }.onFailure { error ->
                _state.update {
                    it.copy(
                        cacheStatusMessage = error.localizedMessage
                            ?.takeIf(String::isNotBlank)
                            ?.let { message -> "Não foi possível limpar o cache: $message" }
                            ?: "Não foi possível limpar o cache.",
                    )
                }
            }
        }
    }

    fun clearAllCache() {
        viewModelScope.launch {
            runCatching {
                // "Limpar Todos os Dados Locais" tem de limpar o que o aparelho guardou
                // do usuário. O histórico de busca vive em outro armazenamento
                // (`mulletaflix_search_history`) e sobrevivia à limpeza: os termos
                // buscados reapareciam no próximo login do mesmo usuário.
                val userId = authRepository.getSavedUserId().firstOrNull()
                searchHistoryRepository?.clear(userId)

                withContext(ioDispatcher) {
                    context.cacheDir.listFiles()
                        ?.let { files ->
                            cacheEntriesToRemove(files.map { it.name })
                                .mapNotNull { name -> files.firstOrNull { it.name == name } }
                        }
                        ?.forEach { entry ->
                            check(entry.deleteRecursively()) {
                                "Não foi possível remover ${entry.name}"
                            }
                        }
                }
                settingsRepository.clearLocalPreferences()
                logoutUseCase().getOrThrow()
            }.onSuccess {
                _state.update {
                    it.copy(cacheStatusMessage = "Dados locais limpos. Você saiu da conta.")
                }
            }.onFailure { error ->
                _state.update {
                    it.copy(
                        cacheStatusMessage = error.localizedMessage
                            ?.takeIf(String::isNotBlank)
                            ?.let { message -> "Não foi possível concluir a limpeza: $message" }
                            ?: "Não foi possível concluir a limpeza dos dados locais.",
                    )
                }
            }
        }
    }

    fun checkForUpdates(currentVersion: String) {
        val normalizedVersion = currentVersion.trim()
        if (checkAppUpdateUseCase == null) {
            _state.update {
                it.copy(
                    updateErrorMessage = "A verificação de atualizações não está disponível.",
                    updateStatusMessage = null,
                )
            }
            return
        }
        if (normalizedVersion.isBlank()) {
            _state.update {
                it.copy(
                    updateErrorMessage = "Não foi possível identificar a versão instalada.",
                    updateStatusMessage = null,
                )
            }
            return
        }
        if (_state.value.isCheckingUpdate || _state.value.isDownloadingUpdate) return
        _state.update { it.copy(isCheckingUpdate = true, updateErrorMessage = null, updateStatusMessage = null) }
        viewModelScope.launch {
            runCatching { checkAppUpdateUseCase(normalizedVersion) }
                .getOrElse { Result.failure(it) }
                .onSuccess { info ->
                    _state.update {
                        it.copy(
                            isCheckingUpdate = false,
                            updateInfo = info,
                            showUpdateDialog = info.isUpdateAvailable,
                            updateStatusMessage = if (!info.isUpdateAvailable) {
                                "Você já está na versão mais recente (${info.currentVersion})."
                            } else null,
                        )
                    }
                }
                .onFailure { error ->
                    _state.update {
                        it.copy(
                            isCheckingUpdate = false,
                            updateErrorMessage = error.message ?: "Erro ao verificar atualizações.",
                        )
                    }
                }
        }
    }

    fun dismissUpdateDialog() {
        _state.update { it.copy(showUpdateDialog = false) }
    }

    fun clearUpdateMessages() {
        _state.update { it.copy(updateStatusMessage = null, updateErrorMessage = null) }
    }

    fun downloadAndInstallUpdate(install: (java.io.File) -> Boolean) {
        // O botão já fica desabilitado enquanto baixa, mas isso é lido na composição:
        // dois toques no mesmo frame passam os dois. E o segundo download apaga o
        // arquivo que o primeiro já abriu (`AppUpdateDownloader`), então o resultado é
        // uma instalação quebrada. `checkForUpdates` já tinha essa guarda; esta é a
        // mesma, no irmão que faltava.
        if (_state.value.isDownloadingUpdate) return
        val downloadUrl = _state.value.updateInfo?.apkDownloadUrl ?: return
        val versionName = _state.value.updateInfo?.latestVersion ?: "update"
        val downloader = appUpdateDownloader ?: return

        // Marcado antes de lançar a corrotina, pelo mesmo motivo dos outros flags deste
        // app: a janela entre o toque e o primeiro `update` é onde o segundo toque entra.
        _state.update { it.copy(isDownloadingUpdate = true, updateDownloadProgress = 0f, updateErrorMessage = null) }

        viewModelScope.launch {
            downloader.downloadApk(
                downloadUrl = downloadUrl,
                versionName = versionName,
                expectedSha256 = _state.value.updateInfo?.apkSha256,
            ).collect { downloadState ->
                when (downloadState) {
                    is DownloadState.Downloading -> {
                        _state.update { it.copy(updateDownloadProgress = downloadState.progress) }
                    }
                    is DownloadState.Completed -> {
                        // A instalação é classificada pela política compartilhada
                        // (`installDownloadedApk`), a mesma da checagem automática da
                        // `MainActivity`; o que esta tela faz com o resultado — fechar o
                        // diálogo e anunciar o status — é dela.
                        when (val outcome = installDownloadedApk(downloadState.file, install)) {
                            AppUpdateInstallOutcome.Started -> {
                                _state.update {
                                    it.copy(
                                        isDownloadingUpdate = false,
                                        showUpdateDialog = false,
                                        updateStatusMessage = "Download concluído. Iniciando instalação...",
                                    )
                                }
                            }

                            else -> {
                                _state.update {
                                    it.copy(
                                        isDownloadingUpdate = false,
                                        updateErrorMessage = outcome.errorMessageOrNull(),
                                    )
                                }
                            }
                        }
                    }
                    is DownloadState.Error -> {
                        _state.update {
                            it.copy(
                                isDownloadingUpdate = false,
                                updateErrorMessage = downloadState.message
                            )
                        }
                    }
                    DownloadState.Idle -> Unit
                }
            }
        }
    }

    // No label/code tables live here any more. Every one of them used to be a
    // second copy of a list stored elsewhere — languages, sort fields, grid
    // densities, subtitle colours — and every drift between the copy and the
    // catalogue produced the same bug: the screen displayed one value and
    // stored another. The catalogues in SettingsOptionLists.kt and the domain
    // models are now the only copies, and SettingsOptionListsTest fails if a
    // dialog stops offering a value the app can store.
}

private fun librarySortLabel(value: String?): String = LibrarySortField.fromCode(value).label

private fun librarySortCode(label: String): String = LibrarySortField.fromLabel(label).code
