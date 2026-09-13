package org.mulletaflix.core.api

import kotlinx.coroutines.flow.Flow

/**
 * Session provider interface used across networking and interceptors.
 */
interface SessionRepository {
    fun getAccessToken(): Flow<String?>
    fun getDeviceId(): Flow<String>
    fun getBaseUrl(): Flow<String>
    fun getCurrentUserId(): Flow<String?>
    suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String)
    suspend fun setBaseUrl(url: String)
    suspend fun clearSession()
}
