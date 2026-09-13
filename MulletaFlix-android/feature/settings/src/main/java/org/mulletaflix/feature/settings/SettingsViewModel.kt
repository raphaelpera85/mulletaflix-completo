package org.mulletaflix.feature.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
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
    val defaultQuality: String = "Automático (1080p)",
    val defaultSpeed: Float = 1.0f,
    val autoPlay: Boolean = true,
    val skipIntro: Boolean = true,
    val subtitleLanguage: String = "Português (Brasil)",
    val subtitleFontSize: Int = 100,
    val downloadPath: String = "Armazenamento Interno",
    val downloadStorageGb: Int = 10,
    val downloadQuality: String = "1080p (Original)",
)

@HiltViewModel
class SettingsViewModel @Inject constructor(
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
            authRepository.getSavedUserId().collect { userId ->
                _state.update { it.copy(username = if (!userId.isNullOrBlank()) "Usuário Ativo" else null) }
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
    }

    fun setSkipIntro(skipIntro: Boolean) {
        _state.update { it.copy(skipIntro = skipIntro) }
    }

    fun logout() {
        viewModelScope.launch {
            authRepository.logout()
        }
    }

    fun clearImageCache() {
        // Cache cleanup
    }

    fun clearAllCache() {
        viewModelScope.launch {
            authRepository.logout()
        }
    }
}
