package org.mulletaflix.feature.itemdetail

import android.content.Context
import org.json.JSONObject
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.readium.r2.shared.publication.Locator

internal data class BookReaderProgressScope(
    val serverIdentity: String,
    val userId: String,
    val itemId: String,
)

internal fun HomeFeedCacheScope.bookReaderProgressScope(itemId: String): BookReaderProgressScope =
    BookReaderProgressScope(
        serverIdentity = serverId?.takeIf { it.isNotBlank() } ?: serverUrl.trimEnd('/'),
        userId = userId,
        itemId = itemId,
    )

internal class BookReaderProgressStore(context: Context) {
    private val preferences = context.getSharedPreferences(PREFERENCES_NAME, Context.MODE_PRIVATE)

    fun read(scope: BookReaderProgressScope): Locator? {
        val key = preferenceKey(scope)
        val raw = preferences.getString(key, null) ?: return null
        return runCatching { Locator.fromJSON(JSONObject(raw)) }
            .getOrElse {
                preferences.edit().remove(key).apply()
                null
            }
    }

    fun write(scope: BookReaderProgressScope, locator: Locator) {
        preferences.edit()
            .putString(preferenceKey(scope), locator.toJSON().toString())
            .apply()
    }

    fun remove(scope: BookReaderProgressScope) {
        preferences.edit().remove(preferenceKey(scope)).apply()
    }

    private fun preferenceKey(scope: BookReaderProgressScope): String = buildString {
        append(scope.serverIdentity.length).append(':').append(scope.serverIdentity)
        append('|').append(scope.userId.length).append(':').append(scope.userId)
        append('|').append(scope.itemId.length).append(':').append(scope.itemId)
    }

    private companion object {
        const val PREFERENCES_NAME = "mulletaflix_book_reader_progress_v1"
    }
}
