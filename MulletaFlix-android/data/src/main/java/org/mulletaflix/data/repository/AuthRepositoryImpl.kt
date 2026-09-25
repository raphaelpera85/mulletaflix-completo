package org.mulletaflix.data.repository

import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.withContext
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.AuthenticateByNameDto
import org.mulletaflix.core.api.dto.QuickConnectDto
import org.mulletaflix.core.api.dto.RegisterUserDto
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.QuickConnectState
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.repository.shouldClearSessionForServerChange
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AuthRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
    private val sessionRepository: SessionRepository,
) : AuthRepository {

    override suspend fun verifyServer(url: String): Result<ServerVerification> = suspendRunCatching {
        val cleanUrl = url.trimEnd('/')
        val previousUrl = sessionRepository.getBaseUrl().first()
        val sessionServerId = sessionRepository.getServerId().first()
        sessionRepository.setBaseUrl(cleanUrl)
        try {
            val startedAt = System.nanoTime()
            val info = api.getPublicSystemInfo()
            // O endereço foi reescrito antes de a verificação terminar. Se o servidor
            // verificado for outro, a sessão guardada (token e usuário) ainda é do
            // servidor anterior e passaria a ser enviada para o host novo: o app
            // acreditaria estar autenticado onde não tem sessão nenhuma.
            if (shouldClearSessionForServerChange(sessionServerId, info.id)) {
                sessionRepository.clearSession()
            }
            ServerVerification(
                name = info.serverName ?: info.productName ?: "MulletaFlix Server",
                version = info.version,
                latencyMs = ((System.nanoTime() - startedAt) / 1_000_000L).coerceAtLeast(0L),
                serverId = info.id,
            )
        } catch (error: Throwable) {
            // `setBaseUrl` é `suspend` e grava no DataStore: numa corrotina já
            // cancelada ele lança antes de escrever, e a restauração não acontecia.
            withContext(NonCancellable) { sessionRepository.setBaseUrl(previousUrl) }
            throw error
        }
    }

    override suspend fun register(username: String, password: String): Result<RegistrationResult> = suspendRunCatching {
        val response = api.registerUser(RegisterUserDto(name = username.trim().lowercase(), password = password))
        RegistrationResult(success = response.success, message = response.message)
    }

    override suspend fun login(username: String, password: String): Result<UserSession> = suspendRunCatching {
        val deviceId = sessionRepository.getDeviceId().first()
        val serverUrl = sessionRepository.getBaseUrl().first()
        val result = api.authenticateByName(AuthenticateByNameDto(username = username, pw = password))

        val token = result.accessToken ?: throw IllegalStateException("Token de acesso não retornado pelo servidor")
        val user = result.user ?: throw IllegalStateException("Usuário não retornado pelo servidor")
        val userId = user.id.takeIf { it.isNotBlank() }
            ?: throw IllegalStateException("ID de usuário inválido")
        val userName = user.name

        sessionRepository.saveSession(
            serverUrl = serverUrl,
            token = token,
            userId = userId,
            userName = userName,
            serverId = result.serverId,
            deviceId = deviceId,
        )

        UserSession(
            userId = userId,
            userName = userName,
            token = token,
            serverId = result.serverId,
        )
    }

    override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = suspendRunCatching {
        api.getPublicUsers().map { user ->
            AvailableUser(
                id = user.id,
                name = user.name,
                primaryImageTag = user.primaryImageTag,
            )
        }
    }

    override suspend fun isQuickConnectEnabled(): Result<Boolean> = suspendRunCatching {
        api.isQuickConnectEnabled()
    }

    override suspend fun initiateQuickConnect(): Result<QuickConnectState> = suspendRunCatching {
        val res = api.initiateQuickConnect()
        val code = res.code.trim()
        val secret = res.secret.trim()
        require(code.isNotEmpty() && secret.isNotEmpty()) {
            "O servidor retornou um código Quick Connect inválido"
        }
        QuickConnectState(
            code = code,
            secret = secret,
            isAuthorized = res.authenticated,
        )
    }

    override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = suspendRunCatching {
        val serverUrl = sessionRepository.getBaseUrl().first()
        val deviceId = sessionRepository.getDeviceId().first()
        val status = api.connectQuickConnect(secret = secret)
        if (!status.authenticated) return@suspendRunCatching null

        val res = api.authenticateWithQuickConnect(QuickConnectDto(secret = secret))
        val token = res.accessToken
        val user = res.user

        if (token != null && user != null) {
            sessionRepository.saveSession(
                serverUrl = serverUrl,
                token = token,
                userId = user.id,
                userName = user.name,
                serverId = res.serverId,
                deviceId = deviceId,
            )
            UserSession(
                userId = user.id,
                userName = user.name,
                token = token,
                serverId = res.serverId,
            )
        } else {
            null
        }
    }

    override suspend fun logout(): Result<Unit> = suspendRunCatching {
        sessionRepository.clearSession()
    }

    override suspend fun getCurrentUserProfile(): Result<org.mulletaflix.domain.model.UserProfile> = suspendRunCatching {
        val userDto = api.getCurrentUser()
        org.mulletaflix.domain.model.UserProfile(
            id = userDto.id,
            name = userDto.name,
            serverId = userDto.serverId,
            primaryImageTag = userDto.primaryImageTag,
            isAdministrator = userDto.policy?.isAdministrator ?: false,
            canDownload = userDto.policy?.enableContentDownloading ?: true,
            canAccessLiveTv = userDto.policy?.enableLiveTvAccess ?: true,
            canPlayMedia = userDto.policy?.enableMediaPlayback ?: true,
            audioLanguagePreference = userDto.configuration?.audioLanguagePreference,
            subtitleLanguagePreference = userDto.configuration?.subtitleLanguagePreference,
        )
    }

    override fun getSavedServerUrl(): Flow<String> = sessionRepository.getBaseUrl()

    override suspend fun setServerUrl(url: String) {
        sessionRepository.setBaseUrl(url)
    }

    override fun getSavedUserId(): Flow<String?> = sessionRepository.getCurrentUserId()

    override fun getSavedUserName(): Flow<String?> = sessionRepository.getCurrentUserName()

    override fun getSavedToken(): Flow<String?> = sessionRepository.getAccessToken()

    override fun getSavedServers(): Flow<List<org.mulletaflix.domain.repository.SavedServer>> =
        sessionRepository.getSavedServers().map { list ->
            list.map { s ->
                org.mulletaflix.domain.repository.SavedServer(
                    name = s.name,
                    url = s.url,
                    latencyMs = s.latencyMs,
                    version = s.version,
                    serverId = s.serverId,
                    lastConnected = s.lastConnected,
                )
            }
        }

    override suspend fun addSavedServer(server: org.mulletaflix.domain.repository.SavedServer) {
        sessionRepository.addSavedServer(
            org.mulletaflix.core.api.SavedServerSession(
                name = server.name,
                url = server.url,
                latencyMs = server.latencyMs,
                version = server.version,
                serverId = server.serverId,
                lastConnected = server.lastConnected,
            )
        )
    }

    override suspend fun removeSavedServer(url: String) {
        sessionRepository.removeSavedServer(url)
    }
}
