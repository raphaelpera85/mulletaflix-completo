package org.mulletaflix.data.repository

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test
import org.mulletaflix.domain.repository.QueuedPlaybackIssue

class PlaybackIssueQueueCodecTest {
    private val adapter = Moshi.Builder()
        .addLast(KotlinJsonAdapterFactory())
        .build()
        .adapter(PlaybackIssueQueueDto::class.java)

    @Test
    fun `queue serialization persists reports but no URL or authentication material`() {
        val issue = QueuedPlaybackIssueDto.from(
            QueuedPlaybackIssue("report-1", "hashed-scope", "movie-1", "Travamentos", "Buffer para", 10L),
        )

        val encoded = adapter.toJson(PlaybackIssueQueueDto(items = listOf(issue)))
        val restored = adapter.fromJson(encoded)

        assertEquals(1, restored?.schemaVersion)
        assertEquals("hashed-scope", restored?.items?.single()?.scopeHash)
        assertEquals("movie-1", restored?.items?.single()?.itemId)
        assertFalse(encoded.contains("accessToken"))
        assertFalse(encoded.contains("serverUrl"))
        assertFalse(encoded.contains("deviceId"))
    }
}
