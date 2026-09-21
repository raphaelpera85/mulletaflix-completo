package org.mulletaflix.data.repository

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
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
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AuthRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
    private val sessionRepository: SessionRepository,
) : AuthRepository {

    override suspend fun verifyServer(url: String): Result<ServerVerification> = runCatching {
        val previousUrl = sessionRepository.getBaseUrl().first()
        sessionRepository.setBaseUrl(url.trimEnd('/'))
        try {
            val startedAt = System.nanoTime()
            val info = api.getPublicSystemInfo()
            ServerVerification(
                name = info.serverName ?: info.productName ?: "MulletaFlix Server",
                version = info.version,
                latencyMs = ((System.nanoTime() - startedAt) / 1_000_000L).coerceAtLeast(0L),
                serverId = info.id,
            )
        } catch (error: Throwable) {
            sessionRepository.setBaseUrl(previousUrl)
            throw error
        }
    }

    override suspend fun register(username: String, password: String): Result<RegistrationResult> = runCatching {
        val response = api.registerUser(RegisterUserDto(name = username.trim().lowercase(), password = password))
        RegistrationResult(success = response.success, message = response.message)
    }

    override suspend fun login(username: String, password: String): Result<UserSession> = runCatching {
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
            deviceId = deviceId,
        )
        sessionRepository.setServerId(result.serverId)

        UserSession(
            userId = userId,
            userName = userName,
            token = token,
            serverId = result.serverId,
        )
    }

    override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = runCatching {
        api.getPublicUsers().map { user ->
            AvailableUser(
                id = user.id,
                name = user.name,
                primaryImageTag = user.primaryImageTag,
            )
        }
    }

    override suspend fun isQuickConnectEnabled(): Result<Boolean> = runCatching {
        api.isQuickConnectEnabled()
    }

    override suspend fun initiateQuickConnect(): Result<QuickConnectState> = runCatching {
        val res = api.initiateQuickConnect()
        QuickConnectState(
            code = res.code,
            secret = res.secret,
            isAuthorized = res.authenticated,
        )
    }

    override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = runCatching {
        val serverUrl = sessionRepository.getBaseUrl().first()
        val deviceId = sessionRepository.getDeviceId().first()
        val status = api.connectQuickConnect(secret = secret)
        if (!status.authenticated) return@runCatching null

        val res = api.authenticateWithQuickConnect(QuickConnectDto(secret = secret))
        val token = res.accessToken
        val user = res.user

        if (token != null && user != null) {
            sessionRepository.saveSession(
                serverUrl = serverUrl,
                token = token,
                userId = user.id,
                userName = user.name,
                deviceId = deviceId,
            )
            sessionRepository.setServerId(res.serverId)
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

    override suspend fun logout(): Result<Unit> = runCatching {
        sessionRepository.clearSession()
    }

    override suspend fun getCurrentUserProfile(): Result<org.mulletaflix.domain.model.UserProfile> = runCatching {
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
