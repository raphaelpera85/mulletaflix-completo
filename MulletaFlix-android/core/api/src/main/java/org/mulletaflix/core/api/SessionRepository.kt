package org.mulletaflix.core.api

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import org.mulletaflix.core.common.session.FeedbackRequestSession

/**
 * Session provider interface used across networking and interceptors.
 */
interface SessionRepository {
    fun getAccessToken(): Flow<String?>
    fun getDeviceId(): Flow<String>
    fun getBaseUrl(): Flow<String>
    fun getCurrentUserId(): Flow<String?>
    /** Captures all credentials used by one feedback request from one session state. */
    fun getFeedbackRequestSession(): Flow<FeedbackRequestSession?> = flowOf(null)
    fun getCurrentUserName(): Flow<String?> = flowOf(null)
    /** Atomic, credential-free identity used to isolate persisted home snapshots. */
    fun getHomeFeedCacheScope(): Flow<HomeFeedCacheScope?> = flowOf(null)
    /** Stable server identity returned by authentication, when available. */
    fun getServerId(): Flow<String?> = flowOf(null)
    suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String)
    suspend fun saveSession(serverUrl: String, token: String, userId: String, userName: String?, deviceId: String) {
        saveSession(serverUrl, token, userId, deviceId)
    }
    suspend fun saveSession(
        serverUrl: String,
        token: String,
        userId: String,
        userName: String?,
        serverId: String?,
        deviceId: String,
    ) {
        saveSession(serverUrl, token, userId, userName, deviceId)
        setServerId(serverId)
    }
    suspend fun setBaseUrl(url: String)
    suspend fun setServerId(serverId: String?) {}
    suspend fun clearSession()
    fun getSavedServers(): Flow<List<SavedServerSession>> = flowOf(emptyList())
    suspend fun addSavedServer(server: SavedServerSession) {}
    suspend fun removeSavedServer(url: String) {}
}

data class HomeFeedCacheScope(val serverId: String?, val serverUrl: String, val userId: String)


data class SavedServerSession(
    val name: String,
    val url: String,
    val latencyMs: Long? = null,
    val version: String? = null,
    val serverId: String? = null,
    val lastConnected: Long = System.currentTimeMillis(),
)
