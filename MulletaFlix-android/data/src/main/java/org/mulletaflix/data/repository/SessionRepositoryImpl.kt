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
    @ApplicationContext private val context: Context,
) : SessionRepository {

    private object PreferencesKeys {
        val SERVER_URL = stringPreferencesKey("server_url")
        val ACCESS_TOKEN = stringPreferencesKey("access_token")
        val USER_ID = stringPreferencesKey("user_id")
        val DEVICE_ID = stringPreferencesKey("device_id")
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

    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.SERVER_URL] = serverUrl.trimEnd('/')
            preferences[PreferencesKeys.ACCESS_TOKEN] = token
            preferences[PreferencesKeys.USER_ID] = userId
            preferences[PreferencesKeys.DEVICE_ID] = deviceId
        }
    }

    override suspend fun setBaseUrl(url: String) {
        context.dataStore.edit { preferences ->
            preferences[PreferencesKeys.SERVER_URL] = url.trimEnd('/')
        }
    }

    override suspend fun clearSession() {
        context.dataStore.edit { preferences ->
            preferences.remove(PreferencesKeys.ACCESS_TOKEN)
            preferences.remove(PreferencesKeys.USER_ID)
        }
    }
}
