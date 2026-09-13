package org.mulletaflix.feature.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.AuthRepository
import javax.inject.Inject

data class ServerInfo(
    val name: String,
    val url: String,
    val latencyMs: Long? = null,
)

data class AuthUser(
    val id: String,
    val name: String,
    val primaryImageTag: String? = null,
)

data class AuthState(
    val serverUrl: String? = null,
    val username: String = "",
    val password: String = "",
    val isLoading: Boolean = false,
    val isAuthenticated: Boolean = false,
    val error: String? = null,
    val savedServers: List<ServerInfo> = emptyList(),
    val availableUsers: List<AuthUser> = emptyList(),
    val quickConnectPin: String? = null,
    val quickConnectSecret: String? = null,
    val isWaitingForQuickConnect: Boolean = false,
)

@HiltViewModel
class AuthViewModel @Inject constructor(
    private val authRepository: AuthRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(AuthState())
    val state: StateFlow<AuthState> = _state.asStateFlow()

    private var quickConnectPollingJob: Job? = null

    init {
        viewModelScope.launch {
            authRepository.getSavedServerUrl().collect { url ->
                if (url.isNotBlank()) {
                    _state.update {
                        it.copy(
                            serverUrl = url,
                            savedServers = listOf(ServerInfo("MulletaFlix Server", url, 12))
                        )
                    }
                }
            }
        }
        viewModelScope.launch {
            combine(
                authRepository.getSavedToken(),
                authRepository.getSavedUserId()
            ) { token, userId ->
                !token.isNullOrBlank() && !userId.isNullOrBlank()
            }.collect { isAuth ->
                _state.update { it.copy(isAuthenticated = isAuth) }
            }
        }
    }

    fun onUsernameChange(newUsername: String) {
        _state.update { it.copy(username = newUsername, error = null) }
    }

    fun onPasswordChange(newPassword: String) {
        _state.update { it.copy(password = newPassword, error = null) }
    }

    fun onUserSelect(user: AuthUser) {
        _state.update { it.copy(username = user.name, error = null) }
    }

    fun connectToServer(url: String, onSuccess: () -> Unit) {
        viewModelScope.launch {
            _state.update { it.copy(isLoading = true, error = null) }
            runCatching {
                val cleanUrl = url.trimEnd('/')
                authRepository.setServerUrl(cleanUrl)
                _state.update {
                    it.copy(
                        isLoading = false,
                        serverUrl = cleanUrl,
                        savedServers = (it.savedServers + ServerInfo("Servidor", cleanUrl, 15)).distinctBy { s -> s.url }
                    )
                }
                onSuccess()
            }.onFailure { err ->
                _state.update { it.copy(isLoading = false, error = err.localizedMessage ?: "Erro ao conectar") }
            }
        }
    }

    fun removeServer(url: String) {
        _state.update { current ->
            current.copy(savedServers = current.savedServers.filterNot { it.url == url })
        }
    }

    fun login() {
        val current = _state.value
        if (current.username.isBlank()) {
            _state.update { it.copy(error = "Digite o nome de usuário") }
            return
        }

        viewModelScope.launch {
            _state.update { it.copy(isLoading = true, error = null) }
            authRepository.login(current.username, current.password)
                .onSuccess {
                    _state.update { it.copy(isLoading = false, isAuthenticated = true) }
                }
                .onFailure { err ->
                    _state.update { it.copy(isLoading = false, error = err.localizedMessage ?: "Falha na autenticação") }
                }
        }
    }

    fun initiateQuickConnect() {
        viewModelScope.launch {
            _state.update { it.copy(isLoading = true, error = null) }
            authRepository.initiateQuickConnect()
                .onSuccess { qc ->
                    _state.update {
                        it.copy(
                            isLoading = false,
                            quickConnectPin = qc.code,
                            quickConnectSecret = qc.secret,
                            isWaitingForQuickConnect = true,
                        )
                    }
                    startQuickConnectPolling(qc.secret)
                }
                .onFailure { err ->
                    _state.update { it.copy(isLoading = false, error = err.localizedMessage ?: "Falha ao iniciar Quick Connect") }
                }
        }
    }

    private fun startQuickConnectPolling(secret: String) {
        quickConnectPollingJob?.cancel()
        quickConnectPollingJob = viewModelScope.launch {
            while (isActive) {
                delay(3000)
                authRepository.checkQuickConnect(secret)
                    .onSuccess { session ->
                        if (session != null) {
                            _state.update {
                                it.copy(
                                    isWaitingForQuickConnect = false,
                                    isAuthenticated = true
                                )
                            }
                            quickConnectPollingJob?.cancel()
                        }
                    }
            }
        }
    }

    fun cancelQuickConnect() {
        quickConnectPollingJob?.cancel()
        _state.update {
            it.copy(
                isWaitingForQuickConnect = false,
                quickConnectPin = null,
                quickConnectSecret = null,
            )
        }
    }
}
