package org.mulletaflix.data.repository

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.AuthenticateByNameDto
import org.mulletaflix.core.api.dto.QuickConnectDto
import org.mulletaflix.core.api.dto.RegisterUserDto
import org.mulletaflix.domain.repository.AuthRepository
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
            val info = api.getPublicSystemInfo()
            ServerVerification(
                name = info.serverName ?: info.productName ?: "MulletaFlix Server",
                version = info.version,
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
        val userId = user.id ?: throw IllegalStateException("ID de usuário inválido")
        val userName = user.name

        sessionRepository.saveSession(
            serverUrl = serverUrl,
            token = token,
            userId = userId,
            deviceId = deviceId,
        )

        UserSession(
            userId = userId,
            userName = userName,
            token = token,
            serverId = result.serverId,
        )
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
        val res = api.connectQuickConnect(QuickConnectDto(secret = secret))
        val token = res.accessToken
        val user = res.user

        if (token != null && user != null) {
            sessionRepository.saveSession(
                serverUrl = serverUrl,
                token = token,
                userId = user.id,
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

    override suspend fun logout(): Result<Unit> = runCatching {
        sessionRepository.clearSession()
    }

    override fun getSavedServerUrl(): Flow<String> = sessionRepository.getBaseUrl()

    override suspend fun setServerUrl(url: String) {
        sessionRepository.setBaseUrl(url)
    }

    override fun getSavedUserId(): Flow<String?> = sessionRepository.getCurrentUserId()

    override fun getSavedToken(): Flow<String?> = sessionRepository.getAccessToken()
}
