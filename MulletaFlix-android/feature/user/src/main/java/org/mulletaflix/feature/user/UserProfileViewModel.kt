package org.mulletaflix.feature.user

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.SettingsRepository
import java.io.File
import javax.inject.Inject

data class UserProfileUiState(
    val isLoading: Boolean = false,
    val userProfile: UserProfile? = null,
    val fallbackUserName: String? = null,
    val fallbackUserId: String? = null,
    val serverUrl: String = "",
    val serverVerification: ServerVerification? = null,
    val availableUsers: List<AvailableUser> = emptyList(),
    val isLoggingOut: Boolean = false,
    val isSwitchingUser: Boolean = false,
    val selectedUserForSwitch: AvailableUser? = null,
    val switchPasswordInput: String = "",
    val isSwitchDialogOpen: Boolean = false,
    val cacheCleared: Boolean = false,
    val cacheSizeFormatted: String = "0 MB",
    val message: String? = null,
    val error: String? = null,
)

@HiltViewModel
class UserProfileViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val sessionRepository: SessionRepository,
    private val settingsRepository: SettingsRepository,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(UserProfileUiState())
    val uiState: StateFlow<UserProfileUiState> = _uiState.asStateFlow()

    init {
        observeSession()
        loadProfile()
    }

    private fun observeSession() {
        viewModelScope.launch {
            combine(
                sessionRepository.getBaseUrl(),
                sessionRepository.getCurrentUserId(),
                sessionRepository.getCurrentUserName(),
            ) { url, id, name ->
                Triple(url, id, name)
            }.collect { (url, id, name) ->
                _uiState.update {
                    it.copy(
                        serverUrl = url,
                        fallbackUserId = id,
                        fallbackUserName = name,
                    )
                }
            }
        }
    }

    fun loadProfile() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            calculateCacheSize()

            // 1. Fetch remote user profile
            val profileResult = authRepository.getCurrentUserProfile()
            if (profileResult.isSuccess) {
                val profile = profileResult.getOrNull()
                _uiState.update { it.copy(userProfile = profile) }
            } else {
                // If remote fetch fails, we retain fallback username/id from local session
                val err = profileResult.exceptionOrNull()
                _uiState.update {
                    it.copy(
                        userProfile = it.fallbackUserId?.let { id ->
                            UserProfile(
                                id = id,
                                name = it.fallbackUserName ?: "Usuário MulletaFlix",
                                isAdministrator = false,
                                canDownload = true,
                                canAccessLiveTv = true,
                                canPlayMedia = true,
                            )
                        }
                    )
                }
            }

            // 2. Fetch server verification / latency
            val currentUrl = _uiState.value.serverUrl
            if (currentUrl.isNotBlank()) {
                authRepository.verifyServer(currentUrl).onSuccess { verification ->
                    _uiState.update { it.copy(serverVerification = verification) }
                }
            }

            // 3. Fetch public users for quick switching
            authRepository.getAvailableUsers().onSuccess { users ->
                val currentId = _uiState.value.userProfile?.id ?: _uiState.value.fallbackUserId
                val otherUsers = users.filter { it.id != currentId }
                _uiState.update { it.copy(availableUsers = otherUsers) }
            }

            _uiState.update { it.copy(isLoading = false) }
        }
    }

    fun selectUserToSwitch(user: AvailableUser?) {
        _uiState.update {
            it.copy(
                selectedUserForSwitch = user,
                switchPasswordInput = "",
                isSwitchDialogOpen = user != null,
                error = null,
            )
        }
    }

    fun onSwitchPasswordChanged(password: String) {
        _uiState.update { it.copy(switchPasswordInput = password) }
    }

    fun confirmSwitchUser(onSuccess: () -> Unit) {
        val targetUser = _uiState.value.selectedUserForSwitch ?: return
        val password = _uiState.value.switchPasswordInput

        viewModelScope.launch {
            _uiState.update { it.copy(isSwitchingUser = true, error = null) }
            authRepository.login(targetUser.name, password)
                .onSuccess {
                    _uiState.update {
                        it.copy(
                            isSwitchingUser = false,
                            isSwitchDialogOpen = false,
                            selectedUserForSwitch = null,
                            switchPasswordInput = "",
                            message = "Sessão iniciada como ${targetUser.name}",
                        )
                    }
                    loadProfile()
                    onSuccess()
                }
                .onFailure { err ->
                    _uiState.update {
                        it.copy(
                            isSwitchingUser = false,
                            error = err.localizedMessage ?: "Senha incorreta ou erro ao alternar usuário",
                        )
                    }
                }
        }
    }

    fun logout(onComplete: () -> Unit) {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoggingOut = true) }
            authRepository.logout()
            _uiState.update { it.copy(isLoggingOut = false) }
            onComplete()
        }
    }

    fun clearCache() {
        viewModelScope.launch {
            try {
                context.cacheDir?.deleteRecursively()
                settingsRepository.clearCache()
                calculateCacheSize()
                _uiState.update {
                    it.copy(
                        cacheCleared = true,
                        message = "Cache limpo com sucesso!",
                    )
                }
            } catch (e: Exception) {
                _uiState.update { it.copy(error = "Erro ao limpar cache: ${e.message}") }
            }
        }
    }

    private fun calculateCacheSize() {
        try {
            val cacheSize = getDirSize(context.cacheDir)
            val sizeMb = cacheSize / (1024.0 * 1024.0)
            val formatted = String.format(java.util.Locale.US, "%.1f MB", sizeMb)
            _uiState.update { it.copy(cacheSizeFormatted = formatted) }
        } catch (_: Exception) {
            _uiState.update { it.copy(cacheSizeFormatted = "0.0 MB") }
        }
    }

    private fun getDirSize(dir: File?): Long {
        if (dir == null || !dir.exists()) return 0L
        var size: Long = 0
        dir.listFiles()?.forEach { file ->
            size += if (file.isDirectory) getDirSize(file) else file.length()
        }
        return size
    }

    fun dismissMessage() {
        _uiState.update { it.copy(message = null, error = null) }
    }
}
