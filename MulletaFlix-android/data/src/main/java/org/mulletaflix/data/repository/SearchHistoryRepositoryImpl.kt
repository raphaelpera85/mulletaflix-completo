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
import javax.inject.Inject
import javax.inject.Singleton

private val Context.searchHistoryDataStore: DataStore<Preferences> by preferencesDataStore(name = "mulletaflix_search_history")

@Singleton
class SearchHistoryRepositoryImpl @Inject constructor(
    @param:ApplicationContext private val context: Context,
) : SearchHistoryRepository {
    override fun observeHistory(userId: String?): Flow<List<String>> =
        context.searchHistoryDataStore.data.map { preferences ->
            decode(preferences[keyFor(userId)])
        }

    override suspend fun add(userId: String?, query: String) {
        val normalized = query.trim()
        if (normalized.isBlank()) return
        context.searchHistoryDataStore.edit { preferences ->
            val updated = (listOf(normalized) + decode(preferences[keyFor(userId)]))
                .distinct()
                .take(MAX_ENTRIES)
            preferences[keyFor(userId)] = JSONArray(updated).toString()
        }
    }

    override suspend fun remove(userId: String?, query: String) {
        context.searchHistoryDataStore.edit { preferences ->
            val updated = decode(preferences[keyFor(userId)]).filterNot { it == query }
            preferences[keyFor(userId)] = JSONArray(updated).toString()
        }
    }

    override suspend fun clear(userId: String?) {
        context.searchHistoryDataStore.edit { preferences -> preferences.remove(keyFor(userId)) }
    }

    private fun keyFor(userId: String?) =
        stringPreferencesKey("history:${userId?.takeIf(String::isNotBlank) ?: ANONYMOUS_USER}")

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
