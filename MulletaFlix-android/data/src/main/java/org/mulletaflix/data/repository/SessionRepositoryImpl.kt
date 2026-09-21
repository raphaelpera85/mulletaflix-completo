package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emitAll
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import org.mulletaflix.core.api.SessionRepository
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "mulletaflix_session")

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
    }

    companion object {
        const val DEFAULT_MULLETAFLIX_SERVER_URL = "http://mulletaflix.duckdns.org:8096"
    }

    override fun getAccessToken(): Flow<String?> {
        return context.dataStore.data.map { preferences ->
            preferences[PreferencesKeys.ACCESS_TOKEN]
        }
    }

    override fun getDeviceId(): Flow<String> {
        return flow {
            val storedId = context.dataStore.data.first()[PreferencesKeys.DEVICE_ID]
            val deviceId = storedId ?: UUID.randomUUID().toString().also { generatedId ->
                context.dataStore.edit { preferences ->
                    preferences[PreferencesKeys.DEVICE_ID] = generatedId
                }
            }
            emit(deviceId)
            emitAll(
                context.dataStore.data.map { preferences ->
                    preferences[PreferencesKeys.DEVICE_ID] ?: deviceId
                }
            )
        }
    }

    override fun getBaseUrl(): Flow<String> {
        return context.dataStore.data.map { preferences ->
            preferences[PreferencesKeys.SERVER_URL] ?: ""
        }
    }

    override fun getCurrentUserId(): Flow<String?> {
        return context.dataStore.data.map { preferences ->
            preferences[PreferencesKeys.USER_ID]
        }
    }

    override fun getCurrentUserName(): Flow<String?> {
        return context.dataStore.data.map { preferences ->
            preferences[PreferencesKeys.USER_NAME]
        }
    }

    override fun getServerId(): Flow<String?> = context.dataStore.data.map { preferences ->
        preferences[PreferencesKeys.SERVER_ID]
    }

    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) {
        saveSession(serverUrl, token, userId, null, deviceId)
    }

    override suspend fun saveSession(serverUrl: String, token: String, userId: String, userName: String?, deviceId: String) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.SERVER_URL] = serverUrl.trimEnd('/')
            preferences[PreferencesKeys.ACCESS_TOKEN] = token
            // Normalized on write as well as on read. This id is embedded in the
            // Media3 download request id (`<userId>::<itemId>`), and the reader
            // trims before stripping that prefix, so storing a padded value would
            // make the two sides disagree and leak the scoped id as a media id.
            preferences[PreferencesKeys.USER_ID] = userId.trim()
            if (!userName.isNullOrBlank()) {
                preferences[PreferencesKeys.USER_NAME] = userName
            } else {
                preferences.remove(PreferencesKeys.USER_NAME)
            }
            preferences[PreferencesKeys.DEVICE_ID] = deviceId
        }
    }

    override suspend fun setBaseUrl(url: String) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.SERVER_URL] = url.trimEnd('/')
        }
    }

    override suspend fun setServerId(serverId: String?) {
        context.dataStore.edit { preferences ->
            if (serverId.isNullOrBlank()) preferences.remove(PreferencesKeys.SERVER_ID)
            else preferences[PreferencesKeys.SERVER_ID] = serverId
        }
    }

    override suspend fun clearSession() {
        context.dataStore.edit { preferences ->
            preferences.remove(PreferencesKeys.ACCESS_TOKEN)
            preferences.remove(PreferencesKeys.USER_ID)
            preferences.remove(PreferencesKeys.USER_NAME)
            preferences.remove(PreferencesKeys.SERVER_ID)
        }
    }

    override fun getSavedServers(): Flow<List<org.mulletaflix.core.api.SavedServerSession>> {
        return context.dataStore.data.map { preferences ->
            val raw = preferences[PreferencesKeys.SAVED_SERVERS]
            deserializeSavedServers(raw)
        }
    }

    override suspend fun addSavedServer(server: org.mulletaflix.core.api.SavedServerSession) {
        val cleanUrl = server.url.trimEnd('/')
        context.dataStore.edit { preferences ->
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
        context.dataStore.edit { preferences ->
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
                            url = obj.getString("url"),
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
                    version = "12.0.2",
                    lastConnected = 0L,
                )
            )
        }
        return list.sortedByDescending { it.lastConnected }
    }
}
