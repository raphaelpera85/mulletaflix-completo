package org.mulletaflix.feature.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.usecase.LoginUseCase
import org.mulletaflix.domain.usecase.RegisterUseCase
import org.mulletaflix.domain.usecase.VerifyServerUseCase
import javax.inject.Inject
import retrofit2.HttpException

const val DEFAULT_MULLETAFLIX_SERVER_URL = "http://mulletaflix.duckdns.org:8096"

data class ServerInfo(
    val name: String,
    val url: String,
    val latencyMs: Long? = null,
    val version: String? = null,
    val serverId: String? = null,
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
    val savedServers: List<ServerInfo> = listOf(
        ServerInfo(
            name = "MulletaFlix Oficial (Nuvem)",
            url = DEFAULT_MULLETAFLIX_SERVER_URL,
            version = "12.0.2",
        )
    ),
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
    private val loginUseCase: LoginUseCase,
    private val registerUseCase: RegisterUseCase,
    private val verifyServerUseCase: VerifyServerUseCase,
) : ViewModel() {

    private val _state = MutableStateFlow(AuthState())
    val state: StateFlow<AuthState> = _state.asStateFlow()

    private var quickConnectPollingJob: Job? = null
    private var usersLoadJob: Job? = null
    private var discoveryJob: Job? = null
    private var connectionJob: Job? = null
    private var connectionGeneration = 0L
    private var usersLoadedForUrl: String? = null
    private var usersLoadingForUrl: String? = null
    private var usersLoadGeneration = 0L

    init {
        discoverLocalServers()
        viewModelScope.launch {
            authRepository.getSavedServerUrl().distinctUntilChanged().collect { url ->
                if (url.isNotBlank()) {
                    _state.update {
                        val hasLocalServer = it.discoveredServers.isNotEmpty()
                        it.copy(
                            serverUrl = if (hasLocalServer) it.serverUrl else url,
                        )
                    }
                    loadAvailableUsers(url)
                }
            }
        }
        viewModelScope.launch {
            authRepository.getSavedServers().collect { servers ->
                if (servers.isNotEmpty()) {
                    _state.update { current ->
                        val mapped = servers.map { s ->
                            ServerInfo(
                                name = s.name,
                                url = s.url,
                                latencyMs = s.latencyMs,
                                version = s.version,
                                serverId = s.serverId,
                            )
                        }
                        current.copy(
                            savedServers = mapped,
                            serverUrl = preferredServerUrl(
                                discovered = current.discoveredServers,
                                saved = mapped,
                                fallback = current.serverUrl,
                            ),
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

    private fun loadAvailableUsers(serverUrl: String) {
        if (usersLoadedForUrl == serverUrl || usersLoadingForUrl == serverUrl) return
        usersLoadJob?.cancel()
        val generation = ++usersLoadGeneration
        usersLoadingForUrl = serverUrl
        usersLoadJob = viewModelScope.launch {
            authRepository.getAvailableUsers()
                .onSuccess { users ->
                    if (generation != usersLoadGeneration || _state.value.serverUrl != serverUrl) return@onSuccess
                    usersLoadedForUrl = serverUrl
                    usersLoadingForUrl = null
                    _state.update {
                        it.copy(
                            availableUsers = users.map { user ->
                                AuthUser(user.id, user.name, user.primaryImageTag)
                            },
                        )
                    }
                }
                .onFailure {
                    // A transient failure must not poison the URL cache: a later
                    // connection attempt needs to be able to try again.
                    if (generation == usersLoadGeneration) {
                        usersLoadedForUrl = null
                        usersLoadingForUrl = null
                    }
                }
        }
    }

    private fun invalidateAvailableUsersForEndpoint() {
        usersLoadJob?.cancel()
        usersLoadJob = null
        usersLoadedForUrl = null
        usersLoadingForUrl = null
        usersLoadGeneration += 1
        _state.update { it.copy(availableUsers = emptyList()) }
    }

    fun discoverLocalServers() {
        discoveryJob?.cancel()
        _state.update { it.copy(isDiscovering = true, error = null) }
        discoveryJob = viewModelScope.launch {
            try {
                val servers = localServerDiscovery.discover()
                _state.update { current ->
                    current.copy(
                        isDiscovering = false,
                        // Prefer the LAN address over the public DuckDNS fallback.
                        // This keeps playback inside the local network whenever the
                        // server advertises itself there.
                        serverUrl = preferredServerUrl(servers, current.savedServers, current.serverUrl),
                        // Keep LAN results even when the URL is already saved.
                        // The UI uses this list to trigger the automatic LAN
                        // connection on every startup.
                        discoveredServers = servers.distinctBy { it.url },
                    )
                }
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (error: Throwable) {
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
        connectionJob?.cancel()
        val generation = ++connectionGeneration
        connectionJob = viewModelScope.launch {
            _state.update { it.copy(isLoading = true, error = null) }
            val cleanUrl = normalizeServerUrl(url)
            if (cleanUrl == null) {
                if (generation != connectionGeneration) return@launch
                _state.update { it.copy(isLoading = false, error = "Informe uma URL HTTP ou HTTPS válida") }
                onFailure()
                return@launch
            }
            // Do not keep showing users from the previous server while this
            // endpoint is being verified or when its verification fails.
            invalidateAvailableUsersForEndpoint()
            verifyServerUseCase(cleanUrl)
                .onSuccess { verification ->
                    if (generation != connectionGeneration) return@onSuccess
                    authRepository.setServerUrl(cleanUrl)
                    authRepository.addSavedServer(
                        org.mulletaflix.domain.repository.SavedServer(
                            name = verification.name,
                            url = cleanUrl,
                            latencyMs = verification.latencyMs,
                            version = verification.version,
                            serverId = verification.serverId,
                        )
                    )
                    _state.update {
                        val newServer = ServerInfo(
                            name = verification.name,
                            url = cleanUrl,
                            latencyMs = verification.latencyMs,
                            version = verification.version,
                            serverId = verification.serverId,
                        )
                        it.copy(
                            isLoading = false,
                            serverUrl = cleanUrl,
                            discoveredServers = it.discoveredServers.filterNot { server -> server.url == cleanUrl },
                            savedServers = (listOf(newServer) + it.savedServers).distinctBy { s -> s.url }
                        )
                    }
                    // The API client now points at the verified endpoint. Refresh
                    // the user picker so it can never show users from a previous
                    // server after a LAN/public endpoint switch.
                    loadAvailableUsers(cleanUrl)
                    onSuccess()
                }
                .onFailure { err ->
                    if (generation != connectionGeneration) return@onFailure
                    _state.update { it.copy(isLoading = false, error = err.localizedMessage ?: "Servidor não encontrado ou indisponível") }
                    onFailure()
                }
        }
    }

    fun removeServer(url: String) {
        viewModelScope.launch {
            authRepository.removeSavedServer(url)
        }
        _state.update { current ->
            current.copy(
                savedServers = current.savedServers.filterNot {
                    it.url == url && it.url != DEFAULT_MULLETAFLIX_SERVER_URL
                }
            )
        }
    }

    fun login(usernameOverride: String? = null, passwordOverride: String? = null) {
        val current = _state.value
        val username = usernameOverride ?: current.username
        val password = passwordOverride ?: current.password
        if (username.isBlank()) {
            _state.update { it.copy(error = "Digite o nome de usuário") }
            return
        }

        viewModelScope.launch {
            _state.update { it.copy(isLoading = true, error = null) }
            loginUseCase(username, password)
                .onSuccess {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isAuthenticated = true,
                            username = username,
                            password = password,
                        )
                    }
                }
                .onFailure { err ->
                    val message = (err as? HttpException)?.let { authenticationErrorMessage(it.code()) }
                        ?: err.localizedMessage
                        ?: "Falha na autenticação"
                    _state.update { it.copy(isLoading = false, error = message) }
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
            registerUseCase(cleanUsername, password)
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
