package org.mulletaflix.core.api

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf

/**
 * Session provider interface used across networking and interceptors.
 */
interface SessionRepository {
    fun getAccessToken(): Flow<String?>
    fun getDeviceId(): Flow<String>
    fun getBaseUrl(): Flow<String>
    fun getCurrentUserId(): Flow<String?>
    fun getCurrentUserName(): Flow<String?> = flowOf(null)
    /** Stable server identity returned by authentication, when available. */
    fun getServerId(): Flow<String?> = flowOf(null)
    suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String)
    suspend fun saveSession(serverUrl: String, token: String, userId: String, userName: String?, deviceId: String) {
        saveSession(serverUrl, token, userId, deviceId)
    }
    suspend fun setBaseUrl(url: String)
    suspend fun setServerId(serverId: String?) {}
    suspend fun clearSession()
    fun getSavedServers(): Flow<List<SavedServerSession>> = flowOf(emptyList())
    suspend fun addSavedServer(server: SavedServerSession) {}
    suspend fun removeSavedServer(url: String) {}
}

data class SavedServerSession(
    val name: String,
    val url: String,
    val latencyMs: Long? = null,
    val version: String? = null,
    val serverId: String? = null,
    val lastConnected: Long = System.currentTimeMillis(),
)

