package org.mulletaflix.core.api

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flowOf
import org.mulletaflix.core.common.session.FeedbackRequestSession

/**
 * Session provider interface used across networking and interceptors.
 */
interface SessionRepository {
    /**
     * Session identity for Activity startup.
     *
     * The default keeps lightweight test repositories source-compatible, but it is only a
     * best-effort combination of independent flows and is not atomic. Persisted repositories
     * used by the application must override this with a single backing-store snapshot.
     */
    fun getSessionState(): Flow<SessionState> = combine(
        getBaseUrl(), getAccessToken(), getCurrentUserId(), getServerId(),
    ) { url, token, userId, serverId -> SessionState(url, token, userId, serverId) }

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

data class SessionState(
    val serverUrl: String,
    val accessToken: String?,
    val userId: String?,
    val serverId: String?,
)

data class HomeFeedCacheScope(val serverId: String?, val serverUrl: String, val userId: String)


data class SavedServerSession(
    val name: String,
    val url: String,
    val latencyMs: Long? = null,
    val version: String? = null,
    val serverId: String? = null,
    val lastConnected: Long = System.currentTimeMillis(),
)
