package org.mulletaflix.data.repository

import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.repository.QueuedPlaybackIssue
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
}
