package org.mulletaflix.feature.auth

import android.os.SystemClock
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import retrofit2.HttpException
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.isActive
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.usecase.LoginUseCase
import org.mulletaflix.domain.usecase.RegisterUseCase
import org.mulletaflix.domain.usecase.VerifyServerUseCase
import javax.inject.Inject

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
            // The public endpoint can be updated independently of the APK.
            // Show a version only after the server handshake verifies it;
            // keeping a historical value here misleads users before connect.
            version = null,
        )
    ),
    val availableUsers: List<AuthUser> = emptyList(),
    val quickConnectPin: String? = null,
    val quickConnectSecret: String? = null,
    val quickConnectSecondsRemaining: Int? = null,
    val isWaitingForQuickConnect: Boolean = false,
    val isQuickConnectAvailable: Boolean? = null,
    val quickConnectAvailabilityError: String? = null,
    val discoveredServers: List<ServerInfo> = emptyList(),
    val isDiscovering: Boolean = false,
    /** True after the persisted server list has emitted, including an empty list. */
    val savedServersLoaded: Boolean = false,
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
    private var quickConnectGeneration = 0L
    private var quickConnectAvailabilityJob: Job? = null
    private var quickConnectAvailabilityGeneration = 0L
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
                    // A saved public URL must not trigger requests while discovery
                    // has already selected a different LAN endpoint.
                    if (_state.value.serverUrl != url) return@collect
                    loadAvailableUsers(url)
                    loadQuickConnectAvailability(_state.value.serverUrl ?: url)
                }
            }
        }
        viewModelScope.launch {
            authRepository.getSavedServers().collect { servers ->
                _state.update { current ->
                    if (servers.isEmpty()) {
                        current.copy(savedServersLoaded = true)
                    } else {
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
                            savedServersLoaded = true,
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

    private fun loadQuickConnectAvailability(serverUrl: String) {
        quickConnectAvailabilityJob?.cancel()
        val generation = ++quickConnectAvailabilityGeneration
        _state.update {
            it.copy(
                isQuickConnectAvailable = null,
                quickConnectAvailabilityError = null,
            )
        }
        quickConnectAvailabilityJob = viewModelScope.launch {
            authRepository.isQuickConnectEnabled().onSuccess { available ->
                if (generation != quickConnectAvailabilityGeneration || _state.value.serverUrl != serverUrl) return@onSuccess
                _state.update {
                    it.copy(
                        isQuickConnectAvailable = available,
                        quickConnectAvailabilityError = null,
                    )
                }
            }.onFailure { error ->
                if (generation != quickConnectAvailabilityGeneration || _state.value.serverUrl != serverUrl) return@onFailure
                _state.update {
                    it.copy(
                        isQuickConnectAvailable = null,
                        quickConnectAvailabilityError = serverConnectionErrorMessage(error),
                    )
                }
            }
        }
    }

    fun retryQuickConnectAvailability() {
        _state.value.serverUrl?.takeIf(String::isNotBlank)?.let(::loadQuickConnectAvailability)
    }

    fun discoverLocalServers() {
        discoveryJob?.cancel()
        _state.update { it.copy(isDiscovering = true, error = null) }
        discoveryJob = viewModelScope.launch {
            try {
                val servers = localServerDiscovery.discover()
                val current = _state.value
                val selectedUrl = preferredServerUrl(servers, current.savedServers, current.serverUrl)
                val endpointChanged = selectedUrl != current.serverUrl
                if (endpointChanged) {
                    // Discovery may replace the public fallback with a LAN endpoint
                    // before the UI starts verification. Never carry data from the
                    // previous endpoint across that boundary.
                    invalidateAvailableUsersForEndpoint()
                    quickConnectAvailabilityJob?.cancel()
                    quickConnectAvailabilityGeneration += 1
                }
                _state.update { current ->
                    current.copy(
                        isDiscovering = false,
                        // Prefer the LAN address over the public DuckDNS fallback.
                        // This keeps playback inside the local network whenever the
                        // server advertises itself there.
                        serverUrl = selectedUrl,
                        isQuickConnectAvailable = if (endpointChanged) null else current.isQuickConnectAvailable,
                        quickConnectAvailabilityError = if (endpointChanged) null else current.quickConnectAvailabilityError,
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
        cancelQuickConnectPolling()
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
            _state.update {
                it.copy(
                    isQuickConnectAvailable = null,
                    quickConnectAvailabilityError = null,
                )
            }
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
                    loadQuickConnectAvailability(cleanUrl)
                    onSuccess()
                }
                .onFailure { err ->
                    if (generation != connectionGeneration) return@onFailure
                    _state.update { it.copy(isLoading = false, error = serverConnectionErrorMessage(err)) }
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

        // O botão "Entrar" fica desabilitado enquanto autentica, mas isso é lido na
        // composição: dois toques no mesmo frame passam os dois, e o campo de senha
        // ainda tem um segundo caminho (o "Done" do teclado). Duas autenticações
        // criam duas sessões no servidor e deixam a última resposta — inclusive uma
        // falha — sobrescrever a outra. O flag é marcado antes de lançar a corrotina.
        if (_state.value.isLoading) return

        _state.update { it.copy(isLoading = true, error = null) }
        viewModelScope.launch {
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
                        ?: serverConnectionErrorMessage(err)
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

        // Mesma janela da autenticação, com uma consequência pior: o segundo
        // cadastro volta do servidor como "usuário já existe" e escreve esse erro
        // por cima do sucesso que já navegou para a tela seguinte.
        if (_state.value.isRegistering) return

        _state.update { it.copy(isRegistering = true, error = null) }
        viewModelScope.launch {
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
                    _state.update { it.copy(isRegistering = false, error = serverConnectionErrorMessage(err)) }
                }
        }
    }

    fun initiateQuickConnect() {
        if (_state.value.isQuickConnectAvailable == false) {
            cancelQuickConnectPolling()
            _state.update {
                it.copy(error = "Quick Connect está desativado neste servidor. Use usuário e senha.")
            }
            return
        }
        cancelQuickConnectPolling()
        val generation = quickConnectGeneration
        quickConnectPollingJob = viewModelScope.launch {
            _state.update { it.copy(isLoading = true, error = null) }
            authRepository.initiateQuickConnect()
                .onSuccess { qc ->
                    if (!isActive || generation != quickConnectGeneration) return@onSuccess
                    _state.update {
                        it.copy(
                            isLoading = false,
                            quickConnectPin = qc.code,
                            quickConnectSecret = qc.secret,
                            quickConnectSecondsRemaining = quickConnectDurationSeconds(),
                            isWaitingForQuickConnect = true,
                        )
                    }
                    // Some server versions authorize during initiation, so
                    // check immediately; all checks still share the same
                    // deadline and bounded request timeout.
                    pollQuickConnect(qc.secret, generation, pollImmediately = qc.isAuthorized)
                }
                .onFailure { err ->
                    if (isActive && generation == quickConnectGeneration) {
                        _state.update { it.copy(isLoading = false, error = serverConnectionErrorMessage(err)) }
                    }
                }
        }
    }

    private fun cancelQuickConnectPolling() {
        quickConnectGeneration += 1
        quickConnectPollingJob?.cancel()
        quickConnectPollingJob = null
        _state.update {
            it.copy(
                quickConnectPin = null,
                quickConnectSecret = null,
                quickConnectSecondsRemaining = null,
                isWaitingForQuickConnect = false,
                // Cancelling invalidates the request that raised this flag, and
                // both of that request's terminal branches skip the clear once
                // the generation moved on. `initiateQuickConnect` awaits a call
                // the repository wraps in `runCatching`, so a cancelled request
                // surfaces as `onFailure` rather than a cancellation — the
                // spinner used to stay up forever after "Cancelar", leaving the
                // "Entrar" and Quick Connect buttons disabled with no way out
                // short of restarting the app.
                isLoading = false,
            )
        }
    }

    private suspend fun pollQuickConnect(
        secret: String,
        generation: Long,
        pollImmediately: Boolean = false,
    ) = coroutineScope {
        val deadlineMillis = QuickConnectMonotonicClock.nowMillis() + QUICK_CONNECT_DURATION_MILLIS
        val countdownJob = launch {
            while (currentCoroutineContext().isActive && generation == quickConnectGeneration) {
                val remainingMillis = (deadlineMillis - QuickConnectMonotonicClock.nowMillis()).coerceAtLeast(0L)
                val remainingSeconds = quickConnectRemainingSeconds(remainingMillis)
                _state.update { current ->
                    if (generation == quickConnectGeneration && current.isWaitingForQuickConnect) {
                        current.copy(quickConnectSecondsRemaining = remainingSeconds.takeIf { it > 0 })
                    } else {
                        current
                    }
                }
                if (remainingMillis == 0L) break
                delay(minOf(1_000L, remainingMillis))
            }
        }

        try {
            var attempts = 0
            while (
                currentCoroutineContext().isActive &&
                generation == quickConnectGeneration &&
                attempts < QUICK_CONNECT_MAX_POLL_ATTEMPTS
            ) {
                val remainingMillis = deadlineMillis - QuickConnectMonotonicClock.nowMillis()
                if (remainingMillis <= 0L) break
                if (!(pollImmediately && attempts == 0)) {
                    delay(minOf(QUICK_CONNECT_POLL_INTERVAL_MILLIS, remainingMillis))
                }
                if (!currentCoroutineContext().isActive || generation != quickConnectGeneration) return@coroutineScope
                attempts++
                withTimeoutOrNull(QUICK_CONNECT_REQUEST_TIMEOUT_MILLIS) {
                    checkQuickConnect(secret, generation)
                }?.let { if (it) return@coroutineScope }
            }

            if (currentCoroutineContext().isActive && generation == quickConnectGeneration) {
                _state.update {
                    it.copy(
                        isWaitingForQuickConnect = false,
                        quickConnectPin = null,
                        quickConnectSecret = null,
                        quickConnectSecondsRemaining = null,
                        error = QUICK_CONNECT_POLL_TIMEOUT_MESSAGE,
                    )
                }
            }
        } finally {
            countdownJob.cancel()
        }
    }

    /** Returns true when the current Quick Connect attempt reached a terminal state. */
    private suspend fun checkQuickConnect(secret: String, generation: Long): Boolean {
        var terminal = false
        authRepository.checkQuickConnect(secret).fold(
            onSuccess = { session ->
                if (generation == quickConnectGeneration && session != null) {
                    _state.update {
                        it.copy(
                            isWaitingForQuickConnect = false,
                            quickConnectPin = null,
                            quickConnectSecret = null,
                            quickConnectSecondsRemaining = null,
                            isAuthenticated = true,
                        )
                    }
                    terminal = true
                }
            },
            onFailure = { error ->
                if (generation == quickConnectGeneration) {
                    quickConnectTerminalErrorMessage(error)?.let { message ->
                        _state.update {
                            it.copy(
                                isWaitingForQuickConnect = false,
                                quickConnectPin = null,
                                quickConnectSecret = null,
                                quickConnectSecondsRemaining = null,
                                error = message,
                            )
                        }
                        terminal = true
                    }
                }
            },
        )
        return terminal
    }

    fun cancelQuickConnect() {
        cancelQuickConnectPolling()
    }
}

internal const val QUICK_CONNECT_MAX_POLL_ATTEMPTS = 100
internal const val QUICK_CONNECT_POLL_INTERVAL_SECONDS = 3
internal const val QUICK_CONNECT_POLL_INTERVAL_MILLIS = QUICK_CONNECT_POLL_INTERVAL_SECONDS * 1_000L
internal const val QUICK_CONNECT_REQUEST_TIMEOUT_MILLIS = 30_000L
internal const val QUICK_CONNECT_DURATION_MILLIS = QUICK_CONNECT_MAX_POLL_ATTEMPTS * QUICK_CONNECT_POLL_INTERVAL_MILLIS
internal const val QUICK_CONNECT_POLL_TIMEOUT_MESSAGE =
    "Não foi possível confirmar o Quick Connect no prazo. Verifique a conexão e gere um novo código."

internal fun quickConnectDurationSeconds(): Int =
    QUICK_CONNECT_MAX_POLL_ATTEMPTS * QUICK_CONNECT_POLL_INTERVAL_SECONDS

internal fun quickConnectRemainingSeconds(remainingMillis: Long): Int {
    val boundedMillis = remainingMillis.coerceAtLeast(0L)
    return ((boundedMillis + 999L) / 1_000L).toInt()
}

internal object QuickConnectMonotonicClock {
    fun nowMillis(): Long = SystemClock.elapsedRealtime()
}

internal fun quickConnectTerminalErrorMessage(error: Throwable): String? = when {
    error is HttpException && error.code() == 404 ->
        "O código Quick Connect expirou. Gere um novo código."
    error is HttpException && error.code() == 401 ->
        "Quick Connect está desativado ou requer autorização no servidor."
    else -> null
}
