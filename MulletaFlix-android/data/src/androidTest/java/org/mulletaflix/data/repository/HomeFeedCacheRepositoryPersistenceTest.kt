package org.mulletaflix.data.repository

import android.content.Context
import android.content.ContextWrapper
import androidx.datastore.preferences.core.edit
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.first
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
    fun snapshotSurvivesRepositoryRecreationWithPreferencesDataStore() = runBlocking {
        val isolated = isolatedContext()
        val session = HomeFeedCacheTestSessionRepository()
        val resumeItem = MediaItem(id = "resume-1", name = "Duna", type = MediaItemType.Movie)
        val favoriteItem = MediaItem(id = "favorite-1", name = "Fundação", type = MediaItemType.Series)

        try {
            HomeFeedCacheRepositoryImpl(isolated.context, session)
                .write("user-1", listOf(resumeItem), listOf(favoriteItem))

            val restored = HomeFeedCacheRepositoryImpl(isolated.context, session).read("user-1")

            assertEquals(listOf(resumeItem), restored?.resumeItems)
            assertEquals(listOf(favoriteItem), restored?.favoriteItems)
            assertTrue(File(isolated.directory, "datastore/mulletaflix_home_cache.preferences_pb").isFile)
        } finally {
            assertTrue("Isolated Home cache directory should be removed", isolated.directory.deleteRecursively())
        }
    }

    @Test
    fun malformedSnapshotIsIgnoredAndRepositoryCanWriteAgain() = runBlocking {
        val isolated = isolatedContext()
        val store = isolated.context.homeFeedCacheStore
        val snapshotKey = androidx.datastore.preferences.core.stringPreferencesKey("snapshot_v1")
        val session = HomeFeedCacheTestSessionRepository()

        try {
            store.edit { it[snapshotKey] = "{" }
            val repository = HomeFeedCacheRepositoryImpl(isolated.context, session)

            assertNull(repository.read("user-1"))
            assertNull(store.data.first()[snapshotKey])

            val recoveredItem = MediaItem(id = "recovered", name = "Recuperado", type = MediaItemType.Movie)
            repository.write("user-1", resumeItems = listOf(recoveredItem), favoriteItems = null)

            assertEquals(listOf(recoveredItem), repository.read("user-1")?.resumeItems)
        } finally {
            assertTrue("Isolated Home cache directory should be removed", isolated.directory.deleteRecursively())
        }
    }

    private fun isolatedContext(): IsolatedContext {
        val base = ApplicationProvider.getApplicationContext<Context>()
        val directory = File(base.cacheDir, "home-cache-${UUID.randomUUID()}")
        check(directory.mkdirs())
        val context = object : ContextWrapper(base) {
            override fun getApplicationContext(): Context = this
            override fun getFilesDir(): File = directory
        }
        return IsolatedContext(directory, context)
    }

    private data class IsolatedContext(val directory: File, val context: Context)

    private class HomeFeedCacheTestSessionRepository : SessionRepository {
        override fun getAccessToken() = flowOf<String?>(null)
        override fun getDeviceId() = flowOf("test-device")
        override fun getBaseUrl() = flowOf("https://media.example")
        override fun getCurrentUserId() = flowOf("user-1")
        override fun getHomeFeedCacheScope() =
            flowOf(HomeFeedCacheScope("server-1", "https://media.example", "user-1"))

        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
