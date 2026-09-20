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
import org.mulletaflix.core.common.update.AppUpdateInstaller
import org.mulletaflix.core.common.update.DownloadState
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.usecase.CheckAppUpdateUseCase
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.VerifyServerUseCase
import javax.inject.Inject

private const val LIBRARY_GRID_DENSITY_COMFORTABLE = "COMFORTABLE"
private const val LIBRARY_GRID_DENSITY_COMPACT = "COMPACT"
private const val LIBRARY_SORT_NAME = "SortName"
private const val LIBRARY_SORT_DATE_ADDED = "DateCreated"
private const val LIBRARY_SORT_RELEASE_DATE = "PremiereDate"
private const val LIBRARY_SORT_RUNTIME = "Runtime"
private const val LIBRARY_SORT_RATING = "CommunityRating"

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
    val librarySort: String = "Nome A-Z",
    val downloadPath: String = "Armazenamento Interno",
    val downloadStorageGb: Int = 10,
    val downloadQuality: String = "1080p (Original)",
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
    @param:IoDispatcher private val ioDispatcher: CoroutineDispatcher = Dispatchers.IO,
) : ViewModel() {

    private val _state = MutableStateFlow(SettingsState())
    val state: StateFlow<SettingsState> = _state.asStateFlow()

    init {
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
                val variant = when (domainTheme) {
                    AppThemeSetting.Dark -> MulletaFlixThemeVariant.Dark
                    AppThemeSetting.Light -> MulletaFlixThemeVariant.Light
                    AppThemeSetting.Netflix -> MulletaFlixThemeVariant.Netflix
                    AppThemeSetting.PurpleHaze -> MulletaFlixThemeVariant.PurpleHaze
                    AppThemeSetting.BlueRadiance -> MulletaFlixThemeVariant.BlueRadiance
                    AppThemeSetting.Wmc -> MulletaFlixThemeVariant.WMC
                    AppThemeSetting.AppleTv -> MulletaFlixThemeVariant.AppleTV
                    AppThemeSetting.DynamicColor -> MulletaFlixThemeVariant.System
                }
                _state.update { it.copy(theme = variant) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getPreferredAudioLanguage().collect { language ->
                _state.update { it.copy(audioLanguage = audioLabel(language)) }
            }
        }
        viewModelScope.launch {
            settingsRepository.getPreferredSubtitleLanguage().collect { language ->
                _state.update { it.copy(subtitleLanguage = subtitleLabel(language)) }
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
                _state.update { it.copy(subtitleFontSize = normalizeSubtitleFontSize(size)) }
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
    }

    fun setTheme(theme: MulletaFlixThemeVariant) {
        _state.update { it.copy(theme = theme) }
        viewModelScope.launch {
            val domainTheme = when (theme) {
                MulletaFlixThemeVariant.System -> AppThemeSetting.DynamicColor
                MulletaFlixThemeVariant.Dark -> AppThemeSetting.Dark
                MulletaFlixThemeVariant.Light -> AppThemeSetting.Light
                MulletaFlixThemeVariant.Netflix -> AppThemeSetting.Netflix
                MulletaFlixThemeVariant.PurpleHaze -> AppThemeSetting.PurpleHaze
                MulletaFlixThemeVariant.BlueRadiance -> AppThemeSetting.BlueRadiance
                MulletaFlixThemeVariant.WMC -> AppThemeSetting.Wmc
                MulletaFlixThemeVariant.AppleTV -> AppThemeSetting.AppleTv
            }
            settingsRepository.setTheme(domainTheme)
        }
    }

    fun setAutoPlay(autoPlay: Boolean) {
        _state.update { it.copy(autoPlay = autoPlay) }
        viewModelScope.launch { settingsRepository.setAutoPlayEnabled(autoPlay) }
    }

    fun setSubtitleLanguage(language: String) {
        _state.update { it.copy(subtitleLanguage = subtitleLabel(language)) }
        viewModelScope.launch {
            settingsRepository.setPreferredSubtitleLanguage(languageCode(language))
        }
    }

    fun setAudioLanguage(language: String) {
        _state.update { it.copy(audioLanguage = audioLabel(language)) }
        viewModelScope.launch {
            settingsRepository.setPreferredAudioLanguage(audioLanguageCode(language))
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
        val normalized = normalizeSubtitleFontSize(size)
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

    fun downloadAndInstallUpdate(context: Context) {
        val downloadUrl = _state.value.updateInfo?.apkDownloadUrl ?: return
        val versionName = _state.value.updateInfo?.latestVersion ?: "update"
        val downloader = appUpdateDownloader ?: return

        viewModelScope.launch {
            _state.update { it.copy(isDownloadingUpdate = true, updateDownloadProgress = 0f, updateErrorMessage = null) }
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
                        val installationStarted = runCatching {
                            AppUpdateInstaller.installApk(context, downloadState.file)
                        }.getOrElse { error ->
                            _state.update {
                                it.copy(
                                    isDownloadingUpdate = false,
                                    updateErrorMessage = error.localizedMessage
                                        ?: "Não foi possível abrir o instalador do APK.",
                                )
                            }
                            false
                        }
                        if (installationStarted) {
                            _state.update {
                                it.copy(
                                    isDownloadingUpdate = false,
                                    showUpdateDialog = false,
                                    updateStatusMessage = "Download concluído. Iniciando instalação...",
                                )
                            }
                        } else if (_state.value.updateErrorMessage == null) {
                            _state.update {
                                it.copy(
                                    isDownloadingUpdate = false,
                                    updateErrorMessage = "Permita a instalação de fontes desconhecidas e tente novamente.",
                                )
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

    private fun subtitleLabel(language: String?): String = when (language?.lowercase()) {
        "por", "pt", "pt-br", "português (brasil)" -> "Português (Brasil)"
        "eng", "en", "english" -> "English"
        "off", "none", "desativadas" -> "Desativadas"
        else -> "Idioma original"
    }

    private fun audioLabel(language: String?): String = when (language?.lowercase()) {
        "por", "pt", "pt-br", "português (brasil)" -> "Português (Brasil)"
        "eng", "en", "english" -> "English"
        "original", "idioma original" -> "Idioma original"
        else -> "Idioma original"
    }

    private fun languageCode(label: String): String = when (label) {
        "Português (Brasil)" -> "por"
        "English" -> "eng"
        "Desativadas" -> "off"
        else -> "original"
    }

    private fun audioLanguageCode(label: String): String = when (label) {
        "Português (Brasil)" -> "por"
        "English" -> "eng"
        else -> "original"
    }

    private fun subtitleColorLabel(color: String?): String = when (color?.trim()?.uppercase()) {
        "YELLOW" -> "Amarelo"
        "CYAN" -> "Ciano"
        else -> "Branco"
    }

    private fun subtitleColorCode(label: String): String = when (label) {
        "Amarelo" -> "YELLOW"
        "Ciano" -> "CYAN"
        else -> "WHITE"
    }

    private fun libraryGridDensityLabel(value: String?): String = when (value?.trim()?.uppercase()) {
        LIBRARY_GRID_DENSITY_COMPACT -> "Compacta"
        else -> "Confortável"
    }

    private fun libraryGridDensityCode(label: String): String = when (label) {
        "Compacta" -> LIBRARY_GRID_DENSITY_COMPACT
        else -> LIBRARY_GRID_DENSITY_COMFORTABLE
    }

    private fun librarySortLabel(value: String?): String = when (value?.trim()) {
        LIBRARY_SORT_DATE_ADDED -> "Data de adição"
        LIBRARY_SORT_RELEASE_DATE -> "Data de lançamento"
        LIBRARY_SORT_RUNTIME -> "Duração"
        LIBRARY_SORT_RATING -> "Avaliação"
        else -> "Nome A-Z"
    }

    private fun librarySortCode(label: String): String = when (label) {
        "Data de adição" -> LIBRARY_SORT_DATE_ADDED
        "Data de lançamento" -> LIBRARY_SORT_RELEASE_DATE
        "Duração" -> LIBRARY_SORT_RUNTIME
        "Avaliação" -> LIBRARY_SORT_RATING
        else -> LIBRARY_SORT_NAME
    }
}
