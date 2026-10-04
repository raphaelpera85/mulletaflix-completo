package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.datastore.preferences.core.MutablePreferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import java.security.MessageDigest
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.first
import org.json.JSONObject
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.readium.r2.shared.publication.Locator

private val Context.bookReaderProgressStore by preferencesDataStore(name = "mulletaflix_book_reader_progress")

internal class BookReaderProgressStore(context: Context) {
    private val store = context.applicationContext.bookReaderProgressStore

    suspend fun read(scope: HomeFeedCacheScope, itemId: String): Locator? = recoverBookReaderStorageFailure {
        val key = entryKey(scope, itemId)
        val encoded = store.data.first()[stringPreferencesKey(key)]
        encoded?.let { Locator.fromJSON(JSONObject(it)) }
    }

    suspend fun write(scope: HomeFeedCacheScope, itemId: String, locator: Locator) {
        recoverBookReaderStorageFailure {
            val key = entryKey(scope, itemId)
            store.edit { preferences ->
                preferences[stringPreferencesKey(key)] = locator.toJSON().toString()
                preferences[longPreferencesKey(timestampKey(key))] = System.currentTimeMillis()
                trimOldEntries(preferences)
            }
        }
    }

    suspend fun remove(scope: HomeFeedCacheScope, itemId: String) {
        val key = entryKey(scope, itemId)
        store.edit { preferences ->
            preferences.remove(stringPreferencesKey(key))
            preferences.remove(longPreferencesKey(timestampKey(key)))
        }
    }

    internal fun entryKey(scope: HomeFeedCacheScope, itemId: String): String {
        val serverIdentity = scope.serverId?.takeIf(String::isNotBlank)
            ?.let { "id:$it" }
            ?: "url:${scope.serverUrl.trim().trimEnd('/')}"
        val identity = "$serverIdentity\u0000${scope.userId}\u0000$itemId"
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(identity.toByteArray(Charsets.UTF_8))
            .joinToString("") { byte -> "%02x".format(byte) }
        return "book_reader_progress_$digest"
    }

    private fun timestampKey(entryKey: String) = "${entryKey}_updated"

    private fun trimOldEntries(preferences: MutablePreferences) {
        val entries = preferences.asMap().keys
            .filter { it.name.startsWith(ENTRY_PREFIX) && !it.name.endsWith(UPDATED_SUFFIX) }
            .sortedByDescending { key -> preferences[longPreferencesKey(timestampKey(key.name))] ?: 0L }
        entries.drop(MAX_ENTRIES).forEach { key ->
            preferences.remove(key)
            preferences.remove(longPreferencesKey(timestampKey(key.name)))
        }
    }

    private companion object {
        const val ENTRY_PREFIX = "book_reader_progress_"
        const val UPDATED_SUFFIX = "_updated"
        const val MAX_ENTRIES = 100
    }
}

internal suspend fun <T> recoverBookReaderStorageFailure(block: suspend () -> T): T? = try {
    block()
} catch (cancelled: CancellationException) {
    throw cancelled
} catch (_: Exception) {
    null
}
