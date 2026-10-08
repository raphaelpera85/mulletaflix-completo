package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.datastore.preferences.core.MutablePreferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import java.security.MessageDigest
import java.util.UUID
import kotlin.math.roundToInt
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.first
import org.json.JSONObject
import org.json.JSONArray
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

internal data class BookReaderBookmark(
    val id: String,
    val label: String,
    val locator: Locator,
)

internal data class BookReaderBookmarkWrite(
    val bookmarks: List<BookReaderBookmark>,
    val added: Boolean,
    val limitReached: Boolean,
)

internal class BookReaderProgressStore(context: Context) {
    private val store = context.applicationContext.bookReaderProgressStore

    suspend fun read(scope: HomeFeedCacheScope, itemId: String): Locator? = recoverBookReaderStorageFailure {
        val key = entryKey(scope, itemId)
        val encoded = store.data.first()[stringPreferencesKey(key)]
        encoded?.let {
            val locatorJson = JSONObject(it)
            val migratedJson = ComicBookArchive.migrateLegacyPageLocator(locatorJson)
            val locator = Locator.fromJSON(migratedJson ?: locatorJson)
            if (migratedJson != null && locator != null) {
                store.edit { preferences -> preferences[stringPreferencesKey(key)] = locator.toJSON().toString() }
            }
            locator
        }
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

    suspend fun readBookmarks(scope: HomeFeedCacheScope, itemId: String): List<BookReaderBookmark>? =
        recoverBookReaderStorageFailure {
            val key = bookmarksKey(scope, itemId)
            val preferences = store.data.first()
            val bookmarks = decodeBookmarks(preferences[key])
            if (preferences[key] != null) {
                store.edit { values ->
                    values[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
                    trimOldEntries(values)
                }
            }
            bookmarks
        }

    suspend fun addBookmark(
        scope: HomeFeedCacheScope,
        itemId: String,
        label: String,
        locator: Locator,
    ): BookReaderBookmarkWrite? = recoverBookReaderStorageFailure {
        val key = bookmarksKey(scope, itemId)
        val normalizedLabel = label.trim().replace(Regex("\\s+"), " ").take(MAX_BOOKMARK_LABEL_LENGTH)
            .ifBlank { "Local salvo" }
        val locatorJson = locator.toJSON().toString()
        require(locatorJson.length <= MAX_BOOKMARK_LOCATOR_CHARS) { "Book locator is too large to save." }
        var wasAdded = false
        var limitReached = false
        val preferences = store.edit { values ->
            val current = decodeBookmarks(values[key])
            if (current.any { it.locator.toJSON().toString() == locatorJson }) return@edit
            if (current.size >= MAX_BOOKMARKS_PER_BOOK) {
                limitReached = true
                return@edit
            }
            val bookmark = BookReaderBookmark(
                id = UUID.randomUUID().toString(),
                label = normalizedLabel,
                locator = locator,
            )
            values[key] = encodeBookmarks(listOf(bookmark) + current)
            values[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
            trimOldEntries(values)
            wasAdded = true
        }
        BookReaderBookmarkWrite(
            bookmarks = decodeBookmarks(preferences[key]),
            added = wasAdded,
            limitReached = limitReached,
        )
    }

    suspend fun removeBookmark(scope: HomeFeedCacheScope, itemId: String, bookmarkId: String): Boolean =
        recoverBookReaderStorageFailure {
            val key = bookmarksKey(scope, itemId)
            store.edit { values ->
                val remaining = decodeBookmarks(values[key]).filterNot { it.id == bookmarkId }
                if (remaining.isEmpty()) {
                    values.remove(key)
                    values.remove(longPreferencesKey(timestampKey(key.name)))
                } else {
                    values[key] = encodeBookmarks(remaining)
                    values[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
                }
                trimOldEntries(values)
            }
            true
        } ?: false

    suspend fun renameBookmark(
        scope: HomeFeedCacheScope,
        itemId: String,
        bookmarkId: String,
        label: String,
    ): Boolean {
        val normalizedLabel = label.trim().replace(Regex("\\s+"), " ")
            .take(MAX_BOOKMARK_LABEL_LENGTH)
        if (normalizedLabel.isBlank()) return false

        return recoverBookReaderStorageFailure {
            val key = bookmarksKey(scope, itemId)
            var found = false
            store.edit { values ->
                val current = decodeBookmarks(values[key])
                val updated = current.map { bookmark ->
                    if (bookmark.id == bookmarkId) {
                        found = true
                        bookmark.copy(label = normalizedLabel)
                    } else {
                        bookmark
                    }
                }
                if (found) {
                    values[key] = encodeBookmarks(updated)
                    values[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
                    trimOldEntries(values)
                }
            }
            found
        } ?: false
    }

    suspend fun removeBookmarks(scope: HomeFeedCacheScope, itemId: String) {
        val key = bookmarksKey(scope, itemId)
        store.edit { values ->
            values.remove(key)
            values.remove(longPreferencesKey(timestampKey(key.name)))
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

    suspend fun readSpeechRatePercent(scope: HomeFeedCacheScope): Int =
        recoverBookReaderStorageFailure {
            val key = speechRateKey(scope)
            val saved = store.data.first()[key]
                ?.toIntOrNull()
                ?.let(BookSpeechRate::normalize)
            if (saved != null) {
                store.edit { preferences ->
                    preferences[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
                    trimOldEntries(preferences)
                }
            }
            saved
        } ?: BookSpeechRate.DEFAULT_PERCENT

    suspend fun writeSpeechRatePercent(scope: HomeFeedCacheScope, percent: Int) {
        recoverBookReaderStorageFailure {
            store.edit { preferences ->
                val key = speechRateKey(scope)
                preferences[key] = BookSpeechRate.normalize(percent).toString()
                preferences[longPreferencesKey(timestampKey(key.name))] = System.currentTimeMillis()
                trimOldEntries(preferences)
            }
        }
    }

    suspend fun removeSpeechRatePercent(scope: HomeFeedCacheScope) {
        recoverBookReaderStorageFailure {
            store.edit { preferences ->
                val key = speechRateKey(scope)
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

    private fun speechRateKey(scope: HomeFeedCacheScope) = stringPreferencesKey(
        "${SPEECH_RATE_PREFIX}${entryKey(scope, SPEECH_RATE_SCOPE_ITEM_ID).removePrefix(ENTRY_PREFIX)}",
    )

    private fun bookmarksKey(scope: HomeFeedCacheScope, itemId: String) = stringPreferencesKey(
        "${BOOKMARKS_PREFIX}${entryKey(scope, itemId).removePrefix(ENTRY_PREFIX)}",
    )

    private fun decodeBookmarks(encoded: String?): List<BookReaderBookmark> {
        if (encoded.isNullOrBlank()) return emptyList()
        val json = runCatching { JSONArray(encoded) }.getOrNull() ?: return emptyList()
        return (0 until json.length()).mapNotNull { index ->
            val row = json.optJSONObject(index) ?: return@mapNotNull null
            val locatorJson = row.optJSONObject("locator") ?: return@mapNotNull null
            val locator = Locator.fromJSON(locatorJson) ?: return@mapNotNull null
            val id = row.optString("id").takeIf(String::isNotBlank) ?: return@mapNotNull null
            val label = row.optString("label").trim().take(MAX_BOOKMARK_LABEL_LENGTH)
                .ifBlank { "Local salvo" }
            BookReaderBookmark(id, label, locator)
        }.take(MAX_BOOKMARKS_PER_BOOK)
    }

    private fun encodeBookmarks(bookmarks: List<BookReaderBookmark>): String = JSONArray().apply {
        bookmarks.take(MAX_BOOKMARKS_PER_BOOK).forEach { bookmark ->
            put(
                JSONObject()
                    .put("id", bookmark.id)
                    .put("label", bookmark.label)
                    .put("locator", bookmark.locator.toJSON()),
            )
        }
    }.toString()

    private fun timestampKey(entryKey: String) = "${entryKey}_updated"

    private fun trimOldEntries(preferences: MutablePreferences) {
        trimEntries(preferences, ENTRY_PREFIX, MAX_ENTRIES)
        trimEntries(preferences, FONT_SIZE_PREFIX, MAX_FONT_SIZE_SCOPES)
        trimEntries(preferences, SPEECH_RATE_PREFIX, MAX_FONT_SIZE_SCOPES)
        trimEntries(preferences, BOOKMARKS_PREFIX, MAX_ENTRIES)
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

    internal companion object {
        const val FONT_SIZE_SCOPE_ITEM_ID = "__reader_font_size_preference__"
        const val SPEECH_RATE_SCOPE_ITEM_ID = "__reader_speech_rate_preference__"
        const val ENTRY_PREFIX = "book_reader_progress_"
        const val FONT_SIZE_PREFIX = "book_reader_font_size_"
        const val SPEECH_RATE_PREFIX = "book_reader_speech_rate_"
        const val BOOKMARKS_PREFIX = "book_reader_bookmarks_"
        const val UPDATED_SUFFIX = "_updated"
        const val MAX_ENTRIES = 100
        const val MAX_FONT_SIZE_SCOPES = 100
        const val MAX_BOOKMARKS_PER_BOOK = 20
        const val MAX_BOOKMARK_LABEL_LENGTH = 80
        const val MAX_BOOKMARK_LOCATOR_CHARS = 8_192
    }
}

internal suspend fun <T> recoverBookReaderStorageFailure(block: suspend () -> T): T? = try {
    block()
} catch (cancelled: CancellationException) {
    throw cancelled
} catch (_: Exception) {
    null
}
