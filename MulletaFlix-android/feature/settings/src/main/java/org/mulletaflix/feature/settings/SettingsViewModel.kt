package org.mulletaflix.feature.settings

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.SettingsRepository
import javax.inject.Inject

data class SettingsState(
    val serverUrl: String? = null,
    val username: String? = null,
    val theme: MulletaFlixThemeVariant = MulletaFlixThemeVariant.Dark,
    val defaultQuality: String = "Auto",
    val defaultSpeed: Float = 1.0f,
    val autoPlay: Boolean = true,
    val skipIntro: Boolean = true,
    val pictureInPicture: Boolean = true,
    val subtitleLanguage: String = "Português (Brasil)",
    val subtitleFontSize: Int = 100,
    val downloadPath: String = "Armazenamento Interno",
    val downloadStorageGb: Int = 10,
    val downloadQuality: String = "1080p (Original)",
)

@HiltViewModel
class SettingsViewModel @Inject constructor(
    @ApplicationContext private val context: Context,
    private val settingsRepository: SettingsRepository,
    private val authRepository: AuthRepository,
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
                _state.update { it.copy(defaultQuality = quality) }
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

    fun setSkipIntro(skipIntro: Boolean) {
        _state.update { it.copy(skipIntro = skipIntro) }
        viewModelScope.launch { settingsRepository.setSkipIntroEnabled(skipIntro) }
    }

    fun setPictureInPicture(enabled: Boolean) {
        _state.update { it.copy(pictureInPicture = enabled) }
        viewModelScope.launch { settingsRepository.setPiPEnabled(enabled) }
    }

    fun setDefaultQuality(quality: String) {
        _state.update { it.copy(defaultQuality = quality) }
        viewModelScope.launch { settingsRepository.setDefaultQuality(quality) }
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

    fun logout() {
        viewModelScope.launch {
            authRepository.logout()
        }
    }

    fun clearImageCache() {
        viewModelScope.launch {
            context.cacheDir.resolve("image_cache").deleteRecursively()
            context.cacheDir.resolve("coil").deleteRecursively()
        }
    }

    fun clearAllCache() {
        viewModelScope.launch {
            context.cacheDir.listFiles()
                ?.let { files -> cacheEntriesToRemove(files.map { it.name }).mapNotNull { name -> files.firstOrNull { it.name == name } } }
                ?.forEach { it.deleteRecursively() }
            settingsRepository.clearLocalPreferences()
            authRepository.logout()
        }
    }

    private fun subtitleLabel(language: String?): String = when (language?.lowercase()) {
        "por", "pt", "pt-br" -> "Português (Brasil)"
        "eng", "en" -> "English"
        "off", "none" -> "Desativadas"
        else -> "Idioma original"
    }

    private fun languageCode(label: String): String = when (label) {
        "Português (Brasil)" -> "por"
        "English" -> "eng"
        "Desativadas" -> "off"
        else -> "original"
    }
}
