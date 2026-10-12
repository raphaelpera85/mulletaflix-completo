package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.MutablePreferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.mutablePreferencesOf
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emitAll
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.SessionState
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.common.session.FeedbackRequestSession
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

internal val Context.sessionPreferencesDataStore: DataStore<Preferences> by preferencesDataStore(name = "mulletaflix_session")
private val sessionMutationMutex = Mutex()

@Singleton
class SessionRepositoryImpl @Inject constructor(
    @param:ApplicationContext private val context: Context,
) : SessionRepository {

    private object PreferencesKeys {
        val SERVER_URL = stringPreferencesKey("server_url")
        val ACCESS_TOKEN = stringPreferencesKey("access_token")
        val USER_ID = stringPreferencesKey("user_id")
        val USER_NAME = stringPreferencesKey("user_name")
        val DEVICE_ID = stringPreferencesKey("device_id")
        val SERVER_ID = stringPreferencesKey("server_id")
        val SAVED_SERVERS = stringPreferencesKey("saved_servers")
        val ENCRYPTED_ACCESS_TOKEN = stringPreferencesKey("encrypted_access_token")
    }

    companion object {
        const val DEFAULT_MULLETAFLIX_SERVER_URL = "https://mulletaflix.duckdns.org"
    }

    private suspend fun migrateSessionData() = sessionMutationMutex.withLock {
        context.sessionPreferencesDataStore.edit { preferences ->
            val oldUrl = preferences[PreferencesKeys.SERVER_URL]
            if (oldUrl != null) {
                val migratedUrl = migrateLegacyOfficialServerUrl(oldUrl)
                if (migratedUrl != oldUrl) preferences[PreferencesKeys.SERVER_URL] = migratedUrl
            }
            val rawServers = preferences[PreferencesKeys.SAVED_SERVERS]
            if (!rawServers.isNullOrBlank()) {
                val migratedServers = runCatching {
                    val source = org.json.JSONArray(rawServers)
                    org.json.JSONArray().apply {
                        for (index in 0 until source.length()) {
                            val server = source.getJSONObject(index)
                            val url = server.optString("url")
                            if (url.isNotBlank()) server.put("url", migrateLegacyOfficialServerUrl(url))
                            put(server)
                        }
                    }.toString()
                }.getOrNull()
                if (migratedServers != null && migratedServers != rawServers) {
                    preferences[PreferencesKeys.SAVED_SERVERS] = migratedServers
                }
            }
            val encrypted = preferences[PreferencesKeys.ENCRYPTED_ACCESS_TOKEN]
            val legacyToken = preferences[PreferencesKeys.ACCESS_TOKEN]
            if (encrypted != null && SessionTokenCrypto.decryptOrNull(encrypted) != null) {
                preferences.remove(PreferencesKeys.ACCESS_TOKEN)
            } else if (!legacyToken.isNullOrBlank()) {
                try {
                    preferences[PreferencesKeys.ENCRYPTED_ACCESS_TOKEN] = SessionTokenCrypto.encrypt(legacyToken)
                    preferences.remove(PreferencesKeys.ACCESS_TOKEN)
                } catch (_: Exception) {
                    clearAuthentication(preferences)
                }
            } else if (encrypted != null) {
                preferences.remove(PreferencesKeys.ACCESS_TOKEN)
                preferences.remove(PreferencesKeys.ENCRYPTED_ACCESS_TOKEN)
                clearAuthentication(preferences)
            }
        }
    }

    private fun sessionData(): Flow<Preferences> = flow {
        migrateSessionData()
        emitAll(
            context.sessionPreferencesDataStore.data.map { session ->
                val merged = mutablePreferencesOf()
                session.asMap().forEach { (key, value) -> copyPreference(merged, key, value) }
                val encrypted = session[PreferencesKeys.ENCRYPTED_ACCESS_TOKEN]
                val token = encrypted?.let(SessionTokenCrypto::decryptOrNull)
                if (token != null) merged[PreferencesKeys.ACCESS_TOKEN] = token
                else merged.remove(PreferencesKeys.ACCESS_TOKEN)
                merged
            },
        )
    }

    override fun getAccessToken(): Flow<String?> {
        return sessionData().map { preferences ->
            preferences[PreferencesKeys.ACCESS_TOKEN]
        }
    }

    /**
     * O `DeviceId` é resolvido uma vez por processo.
     *
     * Ver [SingleFlightId]: sem isso, as requisições paralelas do primeiro boot
     * criavam um UUID cada uma e o aparelho se apresentava ao servidor como dois
     * dispositivos diferentes.
     */
    private val deviceId = SingleFlightId(
        read = { context.sessionPreferencesDataStore.data.first()[PreferencesKeys.DEVICE_ID] },
        write = { generatedId ->
            context.sessionPreferencesDataStore.edit { preferences ->
                preferences[PreferencesKeys.DEVICE_ID] = generatedId
            }
        },
        newId = { UUID.randomUUID().toString() },
    )

    override fun getDeviceId(): Flow<String> {
        return flow {
            val resolved = deviceId.get()
            emit(resolved)
            emitAll(
                context.sessionPreferencesDataStore.data.map { preferences ->
                    preferences[PreferencesKeys.DEVICE_ID] ?: resolved
                }
            )
        }
    }

    override fun getBaseUrl(): Flow<String> {
        return sessionData().map { preferences ->
            preferences[PreferencesKeys.SERVER_URL] ?: ""
        }
    }

    override fun getSessionState(): Flow<SessionState> = sessionData().map { preferences ->
        SessionState(
            serverUrl = preferences[PreferencesKeys.SERVER_URL].orEmpty(),
            accessToken = preferences[PreferencesKeys.ACCESS_TOKEN],
            userId = preferences[PreferencesKeys.USER_ID],
            serverId = preferences[PreferencesKeys.SERVER_ID],
        )
    }

    override fun getCurrentUserId(): Flow<String?> {
        return sessionData().map { preferences ->
            preferences[PreferencesKeys.USER_ID]
        }
    }

    override fun getFeedbackRequestSession(): Flow<FeedbackRequestSession?> = flow {
        val resolvedDeviceId = deviceId.get()
        emitAll(sessionData().map { preferences ->
            val serverUrl = preferences[PreferencesKeys.SERVER_URL].orEmpty()
            val accessToken = preferences[PreferencesKeys.ACCESS_TOKEN]
            val userId = preferences[PreferencesKeys.USER_ID]
            val currentDeviceId = preferences[PreferencesKeys.DEVICE_ID] ?: resolvedDeviceId
            if (serverUrl.isBlank() || accessToken.isNullOrBlank() || userId.isNullOrBlank()) {
                null
            } else {
                FeedbackRequestSession(
                    serverUrl = serverUrl,
                    accessToken = accessToken,
                    userId = userId,
                    deviceId = currentDeviceId,
                    serverId = preferences[PreferencesKeys.SERVER_ID],
                )
            }
        })
    }

    override fun getCurrentUserName(): Flow<String?> {
        return sessionData().map { preferences ->
            preferences[PreferencesKeys.USER_NAME]
        }
    }

    override fun getHomeFeedCacheScope(): Flow<HomeFeedCacheScope?> = sessionData().map { preferences ->
        val userId = preferences[PreferencesKeys.USER_ID]
        val serverUrl = preferences[PreferencesKeys.SERVER_URL].orEmpty()
        if (userId.isNullOrBlank() || serverUrl.isBlank()) null
        else HomeFeedCacheScope(
            serverId = preferences[PreferencesKeys.SERVER_ID],
            serverUrl = serverUrl,
            userId = userId,
        )
    }

    override fun getServerId(): Flow<String?> = sessionData().map { preferences ->
        preferences[PreferencesKeys.SERVER_ID]
    }

    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) {
        saveSession(serverUrl, token, userId, null, deviceId)
    }

    override suspend fun saveSession(serverUrl: String, token: String, userId: String, userName: String?, deviceId: String) {
        saveSession(serverUrl, token, userId, userName, null, deviceId)
    }

    override suspend fun saveSession(
        serverUrl: String,
        token: String,
        userId: String,
        userName: String?,
        serverId: String?,
        deviceId: String,
    ) {
        val ciphertext = token.takeIf(String::isNotBlank)?.let(SessionTokenCrypto::encrypt)
        sessionMutationMutex.withLock {
            context.sessionPreferencesDataStore.edit { preferences ->
                preferences[PreferencesKeys.SERVER_URL] = migrateLegacyOfficialServerUrl(serverUrl.trimEnd('/'))
                preferences.remove(PreferencesKeys.ACCESS_TOKEN)
                if (ciphertext == null) preferences.remove(PreferencesKeys.ENCRYPTED_ACCESS_TOKEN)
                else preferences[PreferencesKeys.ENCRYPTED_ACCESS_TOKEN] = ciphertext
                // Normalize IDs because they scope Media3 download request IDs.
                preferences[PreferencesKeys.USER_ID] = userId.trim()
                if (!userName.isNullOrBlank()) preferences[PreferencesKeys.USER_NAME] = userName
                else preferences.remove(PreferencesKeys.USER_NAME)
                preferences[PreferencesKeys.DEVICE_ID] = deviceId
                if (serverId.isNullOrBlank()) preferences.remove(PreferencesKeys.SERVER_ID)
                else preferences[PreferencesKeys.SERVER_ID] = serverId
            }
        }
    }

    override suspend fun setBaseUrl(url: String) {
        context.sessionPreferencesDataStore.edit { preferences ->
            preferences[PreferencesKeys.SERVER_URL] = migrateLegacyOfficialServerUrl(url.trimEnd('/'))
        }
    }

    override suspend fun setServerId(serverId: String?) {
        context.sessionPreferencesDataStore.edit { preferences ->
            if (serverId.isNullOrBlank()) preferences.remove(PreferencesKeys.SERVER_ID)
            else preferences[PreferencesKeys.SERVER_ID] = serverId
        }
    }

    override suspend fun clearSession() {
        sessionMutationMutex.withLock {
            context.sessionPreferencesDataStore.edit { preferences ->
                preferences.remove(PreferencesKeys.ACCESS_TOKEN)
                preferences.remove(PreferencesKeys.ENCRYPTED_ACCESS_TOKEN)
                preferences.remove(PreferencesKeys.USER_ID)
                preferences.remove(PreferencesKeys.USER_NAME)
                preferences.remove(PreferencesKeys.SERVER_ID)
            }
        }
    }

    override fun getSavedServers(): Flow<List<org.mulletaflix.core.api.SavedServerSession>> {
        return sessionData().map { preferences ->
            val raw = preferences[PreferencesKeys.SAVED_SERVERS]
            deserializeSavedServers(raw)
        }
    }

    override suspend fun addSavedServer(server: org.mulletaflix.core.api.SavedServerSession) {
        val cleanUrl = migrateLegacyOfficialServerUrl(server.url.trimEnd('/'))
        context.sessionPreferencesDataStore.edit { preferences ->
            val currentList = deserializeSavedServers(preferences[PreferencesKeys.SAVED_SERVERS]).toMutableList()
            // Remove existing entry for the same URL (ignoring trailing slash)
            currentList.removeAll { it.url.trimEnd('/') == cleanUrl }
            // Add new or updated entry at top with refreshed timestamp
            currentList.add(0, server.copy(url = cleanUrl, lastConnected = System.currentTimeMillis()))
            preferences[PreferencesKeys.SAVED_SERVERS] = serializeSavedServers(currentList)
        }
    }

    override suspend fun removeSavedServer(url: String) {
        val cleanUrl = url.trimEnd('/')
        // Never remove the default official DuckDNS server
        if (cleanUrl == DEFAULT_MULLETAFLIX_SERVER_URL.trimEnd('/')) {
            return
        }
        context.sessionPreferencesDataStore.edit { preferences ->
            val currentList = deserializeSavedServers(preferences[PreferencesKeys.SAVED_SERVERS]).toMutableList()
            currentList.removeAll { it.url.trimEnd('/') == cleanUrl }
            preferences[PreferencesKeys.SAVED_SERVERS] = serializeSavedServers(currentList)
        }
    }

    private fun serializeSavedServers(servers: List<org.mulletaflix.core.api.SavedServerSession>): String {
        val array = org.json.JSONArray()
        servers.forEach { server ->
            val obj = org.json.JSONObject().apply {
                put("name", server.name)
                put("url", server.url)
                if (server.latencyMs != null) put("latencyMs", server.latencyMs)
                if (server.version != null) put("version", server.version)
                if (server.serverId != null) put("serverId", server.serverId)
                put("lastConnected", server.lastConnected)
            }
            array.put(obj)
        }
        return array.toString()
    }

    private fun deserializeSavedServers(raw: String?): List<org.mulletaflix.core.api.SavedServerSession> {
        val list = mutableListOf<org.mulletaflix.core.api.SavedServerSession>()
        if (!raw.isNullOrBlank()) {
            runCatching {
                val array = org.json.JSONArray(raw)
                for (i in 0 until array.length()) {
                    val obj = array.getJSONObject(i)
                    list.add(
                        org.mulletaflix.core.api.SavedServerSession(
                            name = obj.optString("name", "MulletaFlix Server"),
                            url = migrateLegacyOfficialServerUrl(obj.getString("url")),
                            latencyMs = if (obj.has("latencyMs")) obj.getLong("latencyMs") else null,
                            version = if (obj.has("version")) obj.getString("version") else null,
                            serverId = if (obj.has("serverId")) obj.getString("serverId") else null,
                            lastConnected = obj.optLong("lastConnected", 0L),
                        )
                    )
                }
            }
        }
        // ALWAYS ensure DEFAULT_MULLETAFLIX_SERVER_URL is present in the list
        val hasOfficial = list.any { it.url.trimEnd('/') == DEFAULT_MULLETAFLIX_SERVER_URL.trimEnd('/') }
        if (!hasOfficial) {
            list.add(
                org.mulletaflix.core.api.SavedServerSession(
                    name = "MulletaFlix Oficial (Nuvem)",
                    url = DEFAULT_MULLETAFLIX_SERVER_URL,
                    // The cloud server may be updated independently of the APK.
                    // Only a successful server handshake can provide its version.
                    version = null,
                    lastConnected = 0L,
                )
            )
        }
        return list.sortedByDescending { it.lastConnected }
    }
}

@Suppress("UNCHECKED_CAST")
private fun copyPreference(target: MutablePreferences, key: Preferences.Key<*>, value: Any) {
    target[key as Preferences.Key<Any>] = value
}

private fun clearAuthentication(preferences: MutablePreferences) {
    preferences.remove(stringPreferencesKey("access_token"))
    preferences.remove(stringPreferencesKey("encrypted_access_token"))
    preferences.remove(stringPreferencesKey("user_id"))
    preferences.remove(stringPreferencesKey("user_name"))
    preferences.remove(stringPreferencesKey("server_id"))
}

internal fun migrateLegacyOfficialServerUrl(url: String): String {
    val normalized = url.trim().trimEnd('/')
    return if (normalized.equals("http://mulletaflix.duckdns.org:8096", ignoreCase = true)) {
        SessionRepositoryImpl.DEFAULT_MULLETAFLIX_SERVER_URL
    } else {
        url
    }
}
