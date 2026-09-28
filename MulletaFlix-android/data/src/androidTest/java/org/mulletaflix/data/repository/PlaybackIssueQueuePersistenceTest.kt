package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.squareup.moshi.JsonEncodingException
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.repository.QueuedPlaybackIssue
import java.io.File
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class PlaybackIssueQueuePersistenceTest {
    private val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()

    @Test
    fun queuedReportSurvivesRepositoryRecreationAndCanBeRemoved() = runBlocking {
        val report = QueuedPlaybackIssue(
            id = UUID.randomUUID().toString(),
            scopeHash = "one-way-scope-hash",
            itemId = "movie-42",
            category = "Travamentos",
            description = "Reprodução interrompida",
            createdAtEpochMillis = 42L,
        )
        val isolated = isolatedQueue()
        val writer = isolated.repository
        val reader = PlaybackIssueQueueRepositoryImpl(isolated.store, moshi)

        try {
            writer.enqueue(report)
            assertDataStoreUsesIsolatedDirectory(isolated)

            assertEquals(report, reader.pending().single { it.id == report.id })
            reader.remove(report.id)
            assertEquals(false, reader.pending().any { it.id == report.id })
        } finally {
            try {
                reader.remove(report.id)
            } finally {
                isolated.scope.cancel()
                assertTrue("Temporary DataStore directory should be deleted", isolated.directory.deleteRecursively())
            }
        }
    }

    @Test
    fun queueAcceptsFiftyReportsAndRejectsFiftyFirstWithoutChangingQueue() = runBlocking {
        val isolated = isolatedQueue()
        val repository = isolated.repository
        val reports = (1..50).map { testReport(UUID.randomUUID().toString()) }
        val overflow = testReport(UUID.randomUUID().toString())

        try {
            reports.forEach { repository.enqueue(it) }
            assertDataStoreUsesIsolatedDirectory(isolated)
            assertEquals(50, repository.pendingCount.first())

            assertThrows(IllegalArgumentException::class.java) {
                runBlocking { repository.enqueue(overflow) }
            }

            assertEquals(reports.map { it.id }.toSet(), repository.pending().map { it.id }.toSet())
            assertEquals(50, repository.pendingCount.first())
        } finally {
            try {
                (reports + overflow).forEach { repository.remove(it.id) }
            } finally {
                isolated.scope.cancel()
                assertTrue("Temporary DataStore directory should be deleted", isolated.directory.deleteRecursively())
            }
        }
    }

    @Test
    fun malformedStoredJsonIsReportedAndPreserved() = runBlocking {
        val isolated = isolatedQueue()
        val queueKey = stringPreferencesKey("queue_v1")
        val rawMalformedJson = "{ this is not valid JSON"
        val repository = isolated.repository

        try {
            isolated.store.edit { it[queueKey] = rawMalformedJson }
            assertDataStoreUsesIsolatedDirectory(isolated)

            assertEquals(-1, repository.pendingCount.first())
            assertThrows(JsonEncodingException::class.java) {
                runBlocking { repository.pending() }
            }
            assertEquals(rawMalformedJson, isolated.store.data.first()[queueKey])
        } finally {
            try {
                isolated.store.edit { it.remove(queueKey) }
            } finally {
                isolated.scope.cancel()
                assertTrue("Temporary DataStore directory should be deleted", isolated.directory.deleteRecursively())
            }
        }
    }

    private fun testReport(id: String) = QueuedPlaybackIssue(
        id = id,
        scopeHash = "isolated-test-scope",
        itemId = "movie-test",
        category = "Travamentos",
        description = null,
        createdAtEpochMillis = 42L,
    )

    private fun isolatedQueue(): IsolatedQueue {
        val appContext = ApplicationProvider.getApplicationContext<Context>()
        val directory = File(appContext.cacheDir, "playback-queue-test-${UUID.randomUUID()}")
        check(directory.mkdirs())
        val file = File(directory, "mulletaflix_playback_issue_queue.preferences_pb")
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val store = PreferenceDataStoreFactory.create(scope = scope) {
            file
        }
        return IsolatedQueue(
            directory = directory,
            file = file,
            scope = scope,
            store = store,
            repository = PlaybackIssueQueueRepositoryImpl(store, moshi),
        )
    }

    private fun assertDataStoreUsesIsolatedDirectory(isolated: IsolatedQueue) {
        assertTrue(
            "DataStore should create its file inside the temporary directory",
            isolated.file.isFile && isolated.file.parentFile?.canonicalFile == isolated.directory.canonicalFile,
        )
    }

    private data class IsolatedQueue(
        val directory: File,
        val file: File,
        val scope: CoroutineScope,
        val store: DataStore<Preferences>,
        val repository: PlaybackIssueQueueRepositoryImpl,
    )
}
