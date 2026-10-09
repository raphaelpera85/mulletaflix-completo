package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import org.json.JSONArray
import org.mulletaflix.domain.repository.SearchHistoryRepository
import org.mulletaflix.domain.repository.SearchHistoryScope
import java.security.MessageDigest
import javax.inject.Inject
import javax.inject.Singleton

private val Context.searchHistoryDataStore: DataStore<Preferences> by preferencesDataStore(name = "mulletaflix_search_history")

@Singleton
class SearchHistoryRepositoryImpl @Inject constructor(
    @param:ApplicationContext private val context: Context,
) : SearchHistoryRepository {
    override fun observeHistory(scope: SearchHistoryScope): Flow<List<String>> =
        context.searchHistoryDataStore.data.map { preferences ->
            decode(preferences[keyFor(scope)])
        }

    override suspend fun add(scope: SearchHistoryScope, query: String) {
        val normalized = query.trim()
        if (normalized.isBlank()) return
        context.searchHistoryDataStore.edit { preferences ->
            val updated = (listOf(normalized) + decode(preferences[keyFor(scope)]))
                .distinct()
                .take(MAX_ENTRIES)
            preferences[keyFor(scope)] = JSONArray(updated).toString()
        }
    }

    override suspend fun remove(scope: SearchHistoryScope, query: String) {
        context.searchHistoryDataStore.edit { preferences ->
            val updated = decode(preferences[keyFor(scope)]).filterNot { it == query }
            preferences[keyFor(scope)] = JSONArray(updated).toString()
        }
    }

    override suspend fun clear(scope: SearchHistoryScope) {
        context.searchHistoryDataStore.edit { preferences -> preferences.remove(keyFor(scope)) }
    }

    override suspend fun clearAllForUser(userId: String?) {
        val normalizedUser = userId?.trim()?.takeIf(String::isNotEmpty) ?: ANONYMOUS_USER
        context.searchHistoryDataStore.edit { preferences ->
            val keysToRemove = searchHistoryKeysToClear(
                preferences.asMap().keys.mapTo(mutableSetOf()) { it.name },
                normalizedUser,
            )
            preferences.asMap().keys
                .filter { key -> key.name in keysToRemove }
                .forEach { key -> preferences.remove(key) }
        }
    }

    private fun keyFor(scope: SearchHistoryScope) =
        stringPreferencesKey(searchHistoryPreferenceKey(scope))

    private fun decode(raw: String?): List<String> {
        if (raw.isNullOrBlank()) return emptyList()
        return runCatching {
            val json = JSONArray(raw)
            buildList {
                for (index in 0 until json.length()) {
                    json.optString(index).trim().takeIf(String::isNotBlank)?.let(::add)
                }
            }.distinct().take(MAX_ENTRIES)
        }.getOrDefault(emptyList())
    }

    private companion object {
        const val MAX_ENTRIES = 10
        const val ANONYMOUS_USER = "anonymous"
    }
}

internal fun searchHistoryIdentityDigest(identity: String): String =
    MessageDigest.getInstance("SHA-256")
        .digest(identity.toByteArray(Charsets.UTF_8))
        .joinToString(separator = "") { byte -> "%02x".format(byte.toInt() and 0xff) }

internal fun searchHistoryPreferenceKey(scope: SearchHistoryScope): String =
    "${searchHistoryPreferencePrefix(scope.userIdentity)}${searchHistoryIdentityDigest(scope.serverIdentity)}"

internal fun searchHistoryPreferencePrefix(userId: String): String =
    "history:v2:${searchHistoryIdentityDigest(userId)}:"

internal fun searchHistoryKeysToClear(keyNames: Set<String>, userId: String?): Set<String> {
    val normalizedUser = userId?.trim()?.takeIf(String::isNotEmpty) ?: "anonymous"
    val prefix = searchHistoryPreferencePrefix(normalizedUser)
    val legacyKey = "history:$normalizedUser"
    return keyNames.filterTo(mutableSetOf()) { it == legacyKey || it.startsWith(prefix) }
}
