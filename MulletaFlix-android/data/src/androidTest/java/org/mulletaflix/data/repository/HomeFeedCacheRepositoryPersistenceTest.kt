package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import java.io.File
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class HomeFeedCacheRepositoryPersistenceTest {
    @Test
    fun snapshotSurvivesDataStoreAndRepositoryRecreation() = runBlocking {
        val directory = newTemporaryDirectory("home-feed-cache-recreation")
        val file = File(directory, "mulletaflix_home_cache.preferences_pb")
        val firstScope = newDataStoreScope()
        val session = HomeFeedCacheTestSessionRepository()
        val resumeItem = MediaItem(id = "resume-1", name = "Duna", type = MediaItemType.Movie)
        val favoriteItem = MediaItem(id = "favorite-1", name = "Fundação", type = MediaItemType.Series)

        try {
            val firstStore = PreferenceDataStoreFactory.create(scope = firstScope) { file }
            HomeFeedCacheRepositoryImpl(firstStore, session)
                .write("user-1", listOf(resumeItem), listOf(favoriteItem))
            assertTrue("DataStore should persist the snapshot to its file", file.isFile)
        } finally {
            firstScope.cancel()
        }

        val reopenedScope = newDataStoreScope()
        try {
            val reopenedStore = PreferenceDataStoreFactory.create(scope = reopenedScope) { file }
            val restored = HomeFeedCacheRepositoryImpl(reopenedStore, session).read("user-1")

            assertEquals(listOf(resumeItem), restored?.resumeItems)
            assertEquals(listOf(favoriteItem), restored?.favoriteItems)
        } finally {
            reopenedScope.cancel()
            assertTrue("Temporary Home cache directory should be removed", directory.deleteRecursively())
        }
    }

    @Test
    fun malformedSnapshotIsRemovedAndCanBeReplaced() = runBlocking {
        val directory = newTemporaryDirectory("home-feed-cache-corrupt")
        val file = File(directory, "mulletaflix_home_cache.preferences_pb")
        val scope = newDataStoreScope()
        val store = PreferenceDataStoreFactory.create(scope = scope) { file }
        val snapshotKey = stringPreferencesKey("snapshot_v1")
        val repository = HomeFeedCacheRepositoryImpl(store, HomeFeedCacheTestSessionRepository())

        try {
            store.edit { it[snapshotKey] = "{" }

            assertNull(repository.read("user-1"))
            assertNull(store.data.first()[snapshotKey])

            val recoveredItem = MediaItem(id = "recovered", name = "Recuperado", type = MediaItemType.Movie)
            repository.write("user-1", resumeItems = listOf(recoveredItem), favoriteItems = null)

            assertEquals(listOf(recoveredItem), repository.read("user-1")?.resumeItems)
        } finally {
            scope.cancel()
            assertTrue("Temporary Home cache directory should be removed", directory.deleteRecursively())
        }
    }

    @Test
    fun writeFromInactiveUserDoesNotReplaceTheActiveUsersSnapshot() = runBlocking {
        val directory = newTemporaryDirectory("home-feed-cache-scope")
        val file = File(directory, "mulletaflix_home_cache.preferences_pb")
        val scope = newDataStoreScope()
        val store = PreferenceDataStoreFactory.create(scope = scope) { file }
        val activeSession = HomeFeedCacheTestSessionRepository(userId = "user-1")
        val inactiveSession = HomeFeedCacheTestSessionRepository(userId = "user-2")
        val original = MediaItem(id = "active", name = "Ativo", type = MediaItemType.Movie)
        val rejected = MediaItem(id = "inactive", name = "Inativo", type = MediaItemType.Movie)

        try {
            HomeFeedCacheRepositoryImpl(store, activeSession).write("user-1", listOf(original), emptyList())
            HomeFeedCacheRepositoryImpl(store, inactiveSession).write("user-1", listOf(rejected), emptyList())

            assertEquals(
                listOf(original),
                HomeFeedCacheRepositoryImpl(store, activeSession).read("user-1")?.resumeItems,
            )
            assertNull(HomeFeedCacheRepositoryImpl(store, inactiveSession).read("user-1"))
        } finally {
            scope.cancel()
            assertTrue("Temporary Home cache directory should be removed", directory.deleteRecursively())
        }
    }

    @Test
    fun staleCleanupDoesNotRemoveAReplacementWrittenToPreferencesDataStore() = runBlocking {
        val directory = newTemporaryDirectory("home-feed-cache-stale-cleanup")
        val file = File(directory, "mulletaflix_home_cache.preferences_pb")
        val scope = newDataStoreScope()
        val store = PreferenceDataStoreFactory.create(scope = scope) { file }
        val snapshotKey = stringPreferencesKey("snapshot_v1")
        val staleSnapshot = "observed-old-snapshot"
        val replacement = "newer-snapshot"

        try {
            store.edit { it[snapshotKey] = staleSnapshot }
            val valueObservedByTheOldRead = store.data.first()[snapshotKey]
            store.edit { it[snapshotKey] = replacement }

            removeHomeSnapshotIfUnchanged(store, snapshotKey, checkNotNull(valueObservedByTheOldRead))

            assertEquals(replacement, store.data.first()[snapshotKey])
        } finally {
            scope.cancel()
            assertTrue("Temporary Home cache directory should be removed", directory.deleteRecursively())
        }
    }

    private fun newDataStoreScope() = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    private fun newTemporaryDirectory(prefix: String): File {
        val context = ApplicationProvider.getApplicationContext<Context>()
        return File(context.cacheDir, "$prefix-${UUID.randomUUID()}").also { check(it.mkdirs()) }
    }

    private class HomeFeedCacheTestSessionRepository(
        private val userId: String = "user-1",
    ) : SessionRepository {
        override fun getAccessToken() = flowOf<String?>(null)
        override fun getDeviceId() = flowOf("test-device")
        override fun getBaseUrl() = flowOf("https://media.example")
        override fun getCurrentUserId() = flowOf(userId)
        override fun getHomeFeedCacheScope() =
            flowOf(HomeFeedCacheScope("server-1", "https://media.example", userId))

        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
