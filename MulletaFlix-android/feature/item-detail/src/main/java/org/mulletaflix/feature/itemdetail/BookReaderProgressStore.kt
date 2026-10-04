package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.datastore.preferences.core.MutablePreferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import java.security.MessageDigest
import kotlin.math.roundToInt
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.first
import org.json.JSONObject
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.readium.r2.shared.publication.Locator

private val Context.bookReaderProgressStore by preferencesDataStore(name = "mulletaflix_book_reader_progress")

internal object BookReaderFontSize {
    const val DEFAULT_PERCENT = 100
    const val MIN_PERCENT = 80
    const val MAX_PERCENT = 200
    const val STEP_PERCENT = 10

    fun normalize(percent: Int): Int {
        val clamped = percent.coerceIn(MIN_PERCENT, MAX_PERCENT)
        val steps = ((clamped - MIN_PERCENT).toDouble() / STEP_PERCENT).roundToInt()
        return (MIN_PERCENT + steps * STEP_PERCENT).coerceIn(MIN_PERCENT, MAX_PERCENT)
    }

    fun decrease(percent: Int): Int = (normalize(percent) - STEP_PERCENT).coerceAtLeast(MIN_PERCENT)

    fun increase(percent: Int): Int = (normalize(percent) + STEP_PERCENT).coerceAtMost(MAX_PERCENT)
}

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

    suspend fun readFontSizePercent(scope: HomeFeedCacheScope): Int =
        recoverBookReaderStorageFailure {
            val key = fontSizeKey(scope)
            val saved = store.data.first()[key]
                ?.toIntOrNull()
                ?.let(BookReaderFontSize::normalize)
            if (saved != null) {
                store.edit { preferences ->
                    preferences[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
                    trimOldEntries(preferences)
                }
            }
            saved
        } ?: BookReaderFontSize.DEFAULT_PERCENT

    suspend fun writeFontSizePercent(scope: HomeFeedCacheScope, percent: Int) {
        recoverBookReaderStorageFailure {
            store.edit { preferences ->
                val key = fontSizeKey(scope)
                preferences[key] = BookReaderFontSize.normalize(percent).toString()
                preferences[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
                trimOldEntries(preferences)
            }
        }
    }

    suspend fun removeFontSize(scope: HomeFeedCacheScope) {
        recoverBookReaderStorageFailure {
            store.edit { preferences ->
                val key = fontSizeKey(scope)
                preferences.remove(key)
                preferences.remove(longPreferencesKey(timestampKey(key.name)))
            }
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

    private fun fontSizeKey(scope: HomeFeedCacheScope) = stringPreferencesKey(
        "book_reader_font_size_${entryKey(scope, FONT_SIZE_SCOPE_ITEM_ID).removePrefix(ENTRY_PREFIX)}",
    )

    private fun timestampKey(entryKey: String) = "${entryKey}_updated"

    private fun trimOldEntries(preferences: MutablePreferences) {
        trimEntries(preferences, ENTRY_PREFIX, MAX_ENTRIES)
        trimEntries(preferences, FONT_SIZE_PREFIX, MAX_FONT_SIZE_SCOPES)
    }

    private fun trimEntries(preferences: MutablePreferences, prefix: String, maxEntries: Int) {
        val entries = preferences.asMap().keys
            .filter { it.name.startsWith(prefix) && !it.name.endsWith(UPDATED_SUFFIX) }
            .sortedByDescending { key -> preferences[longPreferencesKey(timestampKey(key.name))] ?: 0L }
        entries.drop(maxEntries).forEach { key ->
            preferences.remove(key)
            preferences.remove(longPreferencesKey(timestampKey(key.name)))
        }
    }

    private companion object {
        const val FONT_SIZE_SCOPE_ITEM_ID = "__reader_font_size_preference__"
        const val ENTRY_PREFIX = "book_reader_progress_"
        const val FONT_SIZE_PREFIX = "book_reader_font_size_"
        const val UPDATED_SUFFIX = "_updated"
        const val MAX_ENTRIES = 100
        const val MAX_FONT_SIZE_SCOPES = 100
    }
}

internal suspend fun <T> recoverBookReaderStorageFailure(block: suspend () -> T): T? = try {
    block()
} catch (cancelled: CancellationException) {
    throw cancelled
} catch (_: Exception) {
    null
}
