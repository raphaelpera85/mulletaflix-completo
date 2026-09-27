package org.mulletaflix.data.repository

import android.content.Context
import android.content.ContextWrapper
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import com.squareup.moshi.JsonEncodingException
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
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
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        val writer = PlaybackIssueQueueRepositoryImpl(context, moshi)
        val reader = PlaybackIssueQueueRepositoryImpl(context, moshi)

        try {
            writer.enqueue(report)

            assertEquals(report, reader.pending().single { it.id == report.id })
            reader.remove(report.id)
            assertEquals(false, reader.pending().any { it.id == report.id })
        } finally {
            reader.remove(report.id)
        }
    }

    @Test
    fun queueAcceptsFiftyReportsAndRejectsFiftyFirstWithoutChangingQueue() = runBlocking {
        val isolated = isolatedContext()
        val context = isolated.context
        val repository = PlaybackIssueQueueRepositoryImpl(context, moshi)
        val reports = (1..50).map { testReport(UUID.randomUUID().toString()) }
        val overflow = testReport(UUID.randomUUID().toString())

        try {
            reports.forEach { repository.enqueue(it) }
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
                assertTrue("Temporary DataStore directory should be deleted", isolated.directory.deleteRecursively())
            }
        }
    }

    @Test
    fun malformedStoredJsonIsReportedAndPreserved() = runBlocking {
        val isolated = isolatedContext()
        val context = isolated.context
        val queueKey = stringPreferencesKey("queue_v1")
        val rawMalformedJson = "{ this is not valid JSON"
        val repository = PlaybackIssueQueueRepositoryImpl(context, moshi)

        try {
            context.playbackIssueQueueStore.edit { it[queueKey] = rawMalformedJson }

            assertEquals(-1, repository.pendingCount.first())
            assertThrows(JsonEncodingException::class.java) {
                runBlocking { repository.pending() }
            }
            assertEquals(rawMalformedJson, context.playbackIssueQueueStore.data.first()[queueKey])
        } finally {
            try {
                context.playbackIssueQueueStore.edit { it.remove(queueKey) }
            } finally {
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

    private fun isolatedContext(): IsolatedContext {
        val appContext = ApplicationProvider.getApplicationContext<Context>()
        val directory = File(appContext.cacheDir, "playback-queue-test-${UUID.randomUUID()}")
        check(directory.mkdirs())
        val context = object : ContextWrapper(appContext) {
            override fun getFilesDir(): File = directory
        }
        return IsolatedContext(context, directory)
    }

    private data class IsolatedContext(val context: Context, val directory: File)
}
