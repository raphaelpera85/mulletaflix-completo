package org.mulletaflix.data.repository

import com.squareup.moshi.Moshi
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.emptyPreferences
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertFalse
import org.junit.Test

class HomeFeedCacheScopeTest {
    @Test
    fun `same server id shares cache over lan and public endpoint but not across users`() {
        val lan = homeFeedCacheScope("server-1", "http://192.168.1.20:8096", "user-1")
        val public = homeFeedCacheScope("server-1", "http://mulletaflix.duckdns.org:8096", "user-1")
        val anotherUser = homeFeedCacheScope("server-1", "http://mulletaflix.duckdns.org:8096", "user-2")
        val anotherServer = homeFeedCacheScope("server-2", "http://mulletaflix.duckdns.org:8096", "user-1")

        assertEquals(lan, public)
        assertNotEquals(lan, anotherUser)
        assertNotEquals(lan, anotherServer)
    }

    @Test
    fun `endpoint fallback normalizes trailing slash and is isolated by user`() {
        assertEquals(
            homeFeedCacheScope(null, "http://MULLETAFLIX.duckdns.org:8096/", "user-1"),
            homeFeedCacheScope(null, "http://mulletaflix.duckdns.org:8096", "user-1"),
        )
        assertNotEquals(
            homeFeedCacheScope(null, "http://mulletaflix.duckdns.org:8096", "user-1"),
            homeFeedCacheScope(null, "http://mulletaflix.duckdns.org:8096", "user-2"),
        )
    }

    @Test
    fun `cache dto serializes card fields without stream paths or credentials`() {
        val adapter = Moshi.Builder().build().adapter(HomeCardDto::class.java)
        val dto = HomeCardDto(
            id = "item-1",
            name = "Movie",
            type = "Movie",
            imageTags = mapOf("Primary" to "tag-1"),
        )

        val json = adapter.toJson(dto)
        val restored = adapter.fromJson(json)

        assertEquals(dto, restored)
        assertFalse(json.contains("accessToken", ignoreCase = true))
        assertFalse(json.contains("streamUrl", ignoreCase = true))
        assertFalse(json.contains("mediaSource", ignoreCase = true))
    }

    @Test
    fun `stale cache cleanup preserves a snapshot written after the stale read`() = runTest {
        val key = stringPreferencesKey("snapshot_v1")
        val dataStore = InMemoryHomePreferencesDataStore()
        val staleSnapshot = "snapshot-from-previous-session"
        val currentSnapshot = "snapshot-from-current-session"

        dataStore.edit { it[key] = staleSnapshot }
        val valueObservedBeforeTheConcurrentWrite = dataStore.data.first()[key]
        dataStore.edit { it[key] = currentSnapshot }

        removeHomeSnapshotIfUnchanged(
            dataStore = dataStore,
            key = key,
            observedValue = checkNotNull(valueObservedBeforeTheConcurrentWrite),
        )

        assertEquals(currentSnapshot, dataStore.data.first()[key])
    }
}

private class InMemoryHomePreferencesDataStore : DataStore<Preferences> {
    private val mutex = Mutex()
    private val preferences = MutableStateFlow<Preferences>(emptyPreferences())

    override val data = preferences

    override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences =
        mutex.withLock {
            transform(preferences.value).also { preferences.value = it }
        }
}
