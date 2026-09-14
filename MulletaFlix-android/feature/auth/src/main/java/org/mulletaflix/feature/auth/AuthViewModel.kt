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

const val DEFAULT_MULLETAFLIX_SERVER_URL = "http://mulletaflix.duckdns.org:8096"

data class ServerInfo(
    val name: String,
    val url: String,
    val latencyMs: Long? = null,
    val version: String? = null,
)

data class AuthUser(
    val id: String,
    val name: String,
    val primaryImageTag: String? = null,
)

data class AuthState(
    val serverUrl: String? = DEFAULT_MULLETAFLIX_SERVER_URL,
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
    val discoveredServers: List<ServerInfo> = emptyList(),
    val isDiscovering: Boolean = false,
    val isRegistering: Boolean = false,
)

@HiltViewModel
class AuthViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val localServerDiscovery: LocalServerDiscovery,
) : ViewModel() {

    private val _state = MutableStateFlow(AuthState())
    val state: StateFlow<AuthState> = _state.asStateFlow()

    private var quickConnectPollingJob: Job? = null

    init {
        discoverLocalServers()
        viewModelScope.launch {
            authRepository.getSavedServerUrl().collect { url ->
                if (url.isNotBlank()) {
                    _state.update {
                        val hasLocalServer = it.discoveredServers.isNotEmpty()
                        it.copy(
                            serverUrl = if (hasLocalServer) it.serverUrl else url,
                            savedServers = listOf(ServerInfo("MulletaFlix Server", url))
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

    fun discoverLocalServers() {
        viewModelScope.launch {
            _state.update { it.copy(isDiscovering = true, error = null) }
            runCatching { localServerDiscovery.discover() }
                .onSuccess { servers ->
                    _state.update { current ->
                        current.copy(
                            isDiscovering = false,
                            // Prefer the LAN address over the public DuckDNS fallback.
                            // This keeps playback inside the local network whenever the
                            // server advertises itself there.
                            serverUrl = preferredServerUrl(servers, emptyList(), current.serverUrl),
                            // Keep LAN results even when the URL is already saved.
                            // The UI uses this list to trigger the automatic LAN
                            // connection on every startup.
                            discoveredServers = servers.distinctBy { it.url },
                        )
                    }
                }
                .onFailure { error ->
                    _state.update {
                        it.copy(
                            isDiscovering = false,
                            error = error.localizedMessage?.takeIf(String::isNotBlank)
                                ?: "Não foi possível procurar servidores nesta rede",
                        )
                    }
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

    fun connectToServer(url: String, onSuccess: () -> Unit, onFailure: () -> Unit = {}) {
        viewModelScope.launch {
            _state.update { it.copy(isLoading = true, error = null) }
            val cleanUrl = normalizeServerUrl(url)
            if (cleanUrl == null) {
                _state.update { it.copy(isLoading = false, error = "Informe uma URL HTTP ou HTTPS válida") }
                onFailure()
                return@launch
            }
            authRepository.verifyServer(cleanUrl)
                .onSuccess { verification ->
                authRepository.setServerUrl(cleanUrl)
                _state.update {
                    it.copy(
                        isLoading = false,
                        serverUrl = cleanUrl,
                        discoveredServers = it.discoveredServers.filterNot { server -> server.url == cleanUrl },
                        savedServers = (it.savedServers + ServerInfo(
                            name = verification.name,
                            url = cleanUrl,
                            latencyMs = verification.latencyMs,
                            version = verification.version,
                        )).distinctBy { s -> s.url }
                    )
                }
                onSuccess()
                }
                .onFailure { err ->
                    _state.update { it.copy(isLoading = false, error = err.localizedMessage ?: "Servidor não encontrado ou indisponível") }
                    onFailure()
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

    fun register(username: String, password: String, onSuccess: () -> Unit = {}) {
        val cleanUsername = username.trim().lowercase()
        when {
            cleanUsername.isBlank() -> {
                _state.update { it.copy(error = "Digite um nome de usuário ou e-mail") }
                return
            }
            password.length < 8 -> {
                _state.update { it.copy(error = "A senha deve ter pelo menos 8 caracteres") }
                return
            }
        }

        viewModelScope.launch {
            _state.update { it.copy(isRegistering = true, error = null) }
            authRepository.register(cleanUsername, password)
                .onSuccess { result ->
                    if (result.success) {
                        _state.update { it.copy(isRegistering = false, error = null) }
                        onSuccess()
                    } else {
                        _state.update { it.copy(isRegistering = false, error = result.message ?: "Ocorreu um erro durante o cadastro") }
                    }
                }
                .onFailure { err ->
                    _state.update { it.copy(isRegistering = false, error = err.localizedMessage ?: "Ocorreu um erro durante o cadastro") }
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
