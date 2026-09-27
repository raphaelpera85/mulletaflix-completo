package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.squareup.moshi.JsonClass
import com.squareup.moshi.Moshi
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.QueuedPlaybackIssue
import javax.inject.Inject
import javax.inject.Singleton

internal val Context.playbackIssueQueueStore by preferencesDataStore(name = "mulletaflix_playback_issue_queue")

@JsonClass(generateAdapter = true)
internal data class PlaybackIssueQueueDto(
    val schemaVersion: Int = 1,
    val items: List<QueuedPlaybackIssueDto> = emptyList(),
)

@JsonClass(generateAdapter = true)
internal data class QueuedPlaybackIssueDto(
    val id: String,
    val scopeHash: String,
    val itemId: String,
    val category: String,
    val description: String?,
    val createdAtEpochMillis: Long,
) {
    fun toDomain() = QueuedPlaybackIssue(id, scopeHash, itemId, category, description, createdAtEpochMillis)

    companion object {
        fun from(issue: QueuedPlaybackIssue) = QueuedPlaybackIssueDto(
            id = issue.id,
            scopeHash = issue.scopeHash,
            itemId = issue.itemId,
            category = issue.category,
            description = issue.description,
            createdAtEpochMillis = issue.createdAtEpochMillis,
        )
    }
}

@Singleton
class PlaybackIssueQueueRepositoryImpl @Inject constructor(
    @param:ApplicationContext private val context: Context,
    moshi: Moshi,
) : PlaybackIssueQueue {
    private val queueKey = stringPreferencesKey("queue_v1")
    private val adapter = moshi.adapter(PlaybackIssueQueueDto::class.java)

    /** Emits -1 for unreadable persisted data so callers don't mistake corruption for an empty queue. */
    override val pendingCount: Flow<Int> = context.playbackIssueQueueStore.data.map { preferences ->
        runCatching { decode(preferences[queueKey]).items.size }.getOrDefault(UNREADABLE_QUEUE)
    }

    override suspend fun enqueue(issue: QueuedPlaybackIssue) {
        require(issue.id.isNotBlank() && issue.scopeHash.isNotBlank() && issue.itemId.isNotBlank())
        context.playbackIssueQueueStore.edit { preferences ->
            val current = decode(preferences[queueKey])
            require(current.items.none { it.id == issue.id }) { "Relato já está na fila." }
            require(current.items.size < MAX_QUEUE_SIZE) { "A fila de relatos está cheia. Envie os relatos pendentes primeiro." }
            preferences[queueKey] = adapter.toJson(
                current.copy(items = current.items + QueuedPlaybackIssueDto.from(issue)),
            )
        }
    }

    override suspend fun pending(): List<QueuedPlaybackIssue> =
        decode(context.playbackIssueQueueStore.data.first()[queueKey]).items.map { it.toDomain() }

    override suspend fun remove(id: String) {
        context.playbackIssueQueueStore.edit { preferences ->
            val current = decode(preferences[queueKey])
            preferences[queueKey] = adapter.toJson(current.copy(items = current.items.filterNot { it.id == id }))
        }
    }

    private fun decode(raw: String?): PlaybackIssueQueueDto {
        if (raw == null) return PlaybackIssueQueueDto()
        val decoded = requireNotNull(adapter.fromJson(raw)) { "Fila de relatos inválida." }
        require(decoded.schemaVersion == SCHEMA_VERSION) { "Versão da fila de relatos não suportada." }
        require(decoded.items.size <= MAX_QUEUE_SIZE) { "Fila de relatos excede o limite permitido." }
        return decoded
    }

    private companion object {
        const val SCHEMA_VERSION = 1
        const val MAX_QUEUE_SIZE = 50
        const val UNREADABLE_QUEUE = -1
    }
}
