package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow

data class UserSession(
    val userId: String,
    val userName: String,
    val token: String,
    val serverId: String?,
)

data class AvailableUser(
    val id: String,
    val name: String,
    val primaryImageTag: String? = null,
)

data class QuickConnectState(
    val code: String,
    val secret: String,
    val isAuthorized: Boolean,
)

interface AuthRepository {
    suspend fun verifyServer(url: String): Result<ServerVerification>
    suspend fun register(username: String, password: String): Result<RegistrationResult>
    suspend fun login(username: String, password: String): Result<UserSession>
    suspend fun getAvailableUsers(): Result<List<AvailableUser>>
    suspend fun initiateQuickConnect(): Result<QuickConnectState>
    suspend fun checkQuickConnect(secret: String): Result<UserSession?>
    suspend fun logout(): Result<Unit>
    suspend fun getCurrentUserProfile(): Result<org.mulletaflix.domain.model.UserProfile>
    fun getSavedServerUrl(): Flow<String>
    suspend fun setServerUrl(url: String)
    fun getSavedUserId(): Flow<String?>
    fun getSavedUserName(): Flow<String?>
    fun getSavedToken(): Flow<String?>
}

data class ServerVerification(
    val name: String,
    val version: String?,
    val latencyMs: Long? = null,
)

data class RegistrationResult(
    val success: Boolean,
    val message: String? = null,
)
