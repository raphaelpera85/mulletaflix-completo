package org.mulletaflix.data.repository

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import android.content.Context
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.repository.MediaRequestDraft
import java.io.File
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class MediaRequestDraftStorePersistenceTest {
    @Test
    fun draftSurvivesStoreRecreationAndRemainsIsolatedByAccountScope() = runBlocking {
        val isolated = isolatedStore()
        val scopeA = "a".repeat(64)
        val scopeB = "b".repeat(64)
        val draftA = MediaRequestDraft("Duna", "Filme", "2024", "Legendas")
        val draftB = MediaRequestDraft("Fundação", "Série", "2021", "Temporada nova")

        try {
            isolated.repository.save(scopeA, draftA)
            isolated.repository.save(scopeB, draftB)
            assertTrue(isolated.file.isFile)
            val serialized = isolated.file.readBytes().decodeToString()
            assertTrue("Raw account or credential data must not be persisted", listOf("fake-user", "fake-token", "server.test").none(serialized::contains))
        } finally {
            isolated.scope.cancel()
        }

        val reopenedScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val reopenedStore = PreferenceDataStoreFactory.create(scope = reopenedScope) { isolated.file }
        val reopened = MediaRequestDraftStoreImpl(reopenedStore)
        try {
            assertEquals(draftA, reopened.load(scopeA))
            assertEquals(draftB, reopened.load(scopeB))
            reopened.clear(scopeA)
            assertNull(reopened.load(scopeA))
            assertEquals(draftB, reopened.load(scopeB))
        } finally {
            reopenedScope.cancel()
            assertTrue("Temporary draft directory should be deleted", isolated.directory.deleteRecursively())
        }
    }

    @Test
    fun retainingMoreThanEightScopesEvictsTheOldestDraft() = runBlocking {
        val isolated = isolatedStore()
        try {
            val scopes = (1..9).map { it.toString(16).repeat(64) }
            scopes.forEachIndexed { index, scope ->
                isolated.repository.save(scope, MediaRequestDraft(title = "Item $index"))
            }
            assertNull(isolated.repository.load(scopes.first()))
            assertEquals(MediaRequestDraft(title = "Item 8"), isolated.repository.load(scopes.last()))
        } finally {
            isolated.scope.cancel()
            assertTrue(isolated.directory.deleteRecursively())
        }
    }

    private fun isolatedStore(): IsolatedStore {
        val context = ApplicationProvider.getApplicationContext<Context>()
        val directory = File(context.cacheDir, "media-request-draft-test-${UUID.randomUUID()}")
        check(directory.mkdirs())
        val file = File(directory, "mulletaflix_media_request_drafts.preferences_pb")
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val store = PreferenceDataStoreFactory.create(scope = scope) { file }
        return IsolatedStore(directory, file, scope, store, MediaRequestDraftStoreImpl(store))
    }

    private data class IsolatedStore(
        val directory: File,
        val file: File,
        val scope: CoroutineScope,
        val dataStore: DataStore<Preferences>,
        val repository: MediaRequestDraftStoreImpl,
    )
}
