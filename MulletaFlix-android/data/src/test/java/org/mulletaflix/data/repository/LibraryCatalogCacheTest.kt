package org.mulletaflix.data.repository

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.emptyPreferences
import androidx.datastore.preferences.core.stringPreferencesKey
import com.squareup.moshi.Moshi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.ImageType
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class LibraryCatalogCacheTest {
    @Test
    fun `cache scope is shared across lan and public urls but isolated by server and user`() {
        val lan = libraryCatalogCacheScope("server-1", "http://192.168.1.20:8096", "user-1")
        val public = libraryCatalogCacheScope("server-1", "http://mulletaflix.duckdns.org:8096", "user-1")

        assertEquals(lan, public)
        assertNotEquals(lan, libraryCatalogCacheScope("server-2", "http://mulletaflix.duckdns.org:8096", "user-1"))
        assertNotEquals(lan, libraryCatalogCacheScope("server-1", "http://mulletaflix.duckdns.org:8096", "user-2"))
    }

    @Test
    fun `card snapshot round trips display metadata without any media source or credential fields`() {
        val adapter = Moshi.Builder().build().adapter(LibraryCardSnapshotDto::class.java)
        val card = LibraryCardSnapshotDto(
            id = "media-1",
            name = "Filme",
            type = "Movie",
            overview = "Sinopse",
            year = 2024,
            officialRating = "14",
            genres = listOf("Drama", "Ficção científica"),
            imageTags = mapOf(ImageType.Primary.name to "abc123"),
            hasHD = true,
        )

        val json = adapter.toJson(card)
        val restored = adapter.fromJson(json)

        assertEquals(card, restored)
        assertEquals(listOf("Drama", "Ficção científica"), restored?.genres)
        listOf("mediaSources", "mediaStreams", "transcodeUrl", "directStreamUrl", "path", "accessToken", "api_key")
            .forEach { forbidden -> assertFalse("DTO leaked $forbidden", json.contains(forbidden, ignoreCase = true)) }
        assertFalse(json.contains("http://", ignoreCase = true))
    }

    @Test
    fun `snapshot card count is bounded and malformed identifiers are dropped`() {
        val items = (0..250).map { MediaItem("item-$it", "Title $it", MediaItemType.Movie) } +
            MediaItem("../../private", "Invalid id", MediaItemType.Movie)

        val bounded = librarySnapshotItems(items)

        assertEquals(MAX_LIBRARY_CACHED_ITEMS, bounded.size)
        assertTrue(bounded.all { it.id.matches(Regex("[A-Za-z0-9_-]{1,128}")) })
    }

    @Test
    fun `stale scope cleanup does not delete a newer library snapshot`() = runTest {
        val dataStore = InMemoryLibraryPreferencesDataStore()
        val key = stringPreferencesKey("snapshot")
        val observed = "old-account-snapshot"
        val writtenLater = "new-account-snapshot"
        dataStore.edit { it[key] = observed }
        val staleValue = dataStore.data.first()[key]
        dataStore.edit { it[key] = writtenLater }

        removeLibrarySnapshotIfUnchanged(dataStore, key, checkNotNull(staleValue))

        assertEquals(writtenLater, dataStore.data.first()[key])
    }
}

private class InMemoryLibraryPreferencesDataStore : DataStore<Preferences> {
    private val mutex = Mutex()
    private val values = MutableStateFlow<Preferences>(emptyPreferences())
    override val data = values

    override suspend fun updateData(transform: suspend (Preferences) -> Preferences): Preferences =
        mutex.withLock { transform(values.value).also { values.value = it } }
}
