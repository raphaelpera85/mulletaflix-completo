package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.squareup.moshi.JsonClass
import com.squareup.moshi.Moshi
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.CachedHomeSections
import org.mulletaflix.domain.model.ImageType
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.HomeFeedCache
import java.net.URI
import java.security.MessageDigest
import javax.inject.Inject
import javax.inject.Singleton

private val Context.homeFeedCacheStore by preferencesDataStore(name = "mulletaflix_home_cache")

/** Small versioned DTO deliberately excludes stream URLs, file paths, credentials and source metadata. */
@JsonClass(generateAdapter = true)
internal data class HomeSnapshotDto(
    val schemaVersion: Int = 1,
    val scope: String,
    val resumeSavedAtEpochMillis: Long,
    val favoritesSavedAtEpochMillis: Long,
    val resumeItems: List<HomeCardDto>,
    val favoriteItems: List<HomeCardDto>,
)

@JsonClass(generateAdapter = true)
internal data class HomeCardDto(
    val id: String,
    val name: String,
    val type: String,
    val overview: String? = null,
    val year: Int? = null,
    val premiereDate: String? = null,
    val genres: List<String> = emptyList(),
    val imageTags: Map<String, String> = emptyMap(),
    val backdropImageTags: List<String> = emptyList(),
    val seriesId: String? = null,
    val seriesName: String? = null,
    val seasonName: String? = null,
    val indexNumber: Int? = null,
    val parentIndexNumber: Int? = null,
    val playedPercentage: Double? = null,
    val playbackPositionTicks: Long? = null,
    val primaryImageTag: String? = null,
    val seriesPrimaryImageTag: String? = null,
    val seriesThumbImageTag: String? = null,
    val parentThumbItemId: String? = null,
    val parentThumbImageTag: String? = null,
    val parentBackdropItemId: String? = null,
    val parentBackdropImageTags: List<String> = emptyList(),
)

@Singleton
class HomeFeedCacheRepositoryImpl @Inject constructor(
    @param:ApplicationContext private val context: Context,
    private val sessionRepository: SessionRepository,
) : HomeFeedCache {
    private val snapshotKey = stringPreferencesKey("snapshot_v1")
    private val adapter = Moshi.Builder().build().adapter(HomeSnapshotDto::class.java)
    private val writeMutex = Mutex()

    override suspend fun read(userId: String): CachedHomeSections? {
        val scope = cacheScope(userId) ?: return null
        val preferences = context.homeFeedCacheStore.data.first()
        val encoded = preferences[snapshotKey] ?: return null
        val snapshot = runCatching { adapter.fromJson(encoded) }.getOrNull()
        if (snapshot == null || snapshot.schemaVersion != SCHEMA_VERSION || snapshot.scope != scope) {
            removeHomeSnapshotIfUnchanged(context.homeFeedCacheStore, snapshotKey, encoded)
            return null
        }
        return CachedHomeSections(
            resumeItems = snapshot.resumeItems.map { it.toDomain() },
            favoriteItems = snapshot.favoriteItems.map { it.toDomain() },
            resumeSavedAtEpochMillis = snapshot.resumeSavedAtEpochMillis,
            favoritesSavedAtEpochMillis = snapshot.favoritesSavedAtEpochMillis,
        )
    }

    override suspend fun write(
        userId: String,
        resumeItems: List<MediaItem>?,
        favoriteItems: List<MediaItem>?,
    ) {
        writeMutex.withLock {
            val scope = cacheScope(userId) ?: return
            val current = read(userId)
            val now = System.currentTimeMillis()
            val snapshot = HomeSnapshotDto(
                scope = scope,
                resumeSavedAtEpochMillis = if (resumeItems != null) now else current?.resumeSavedAtEpochMillis ?: 0,
                favoritesSavedAtEpochMillis = if (favoriteItems != null) now else current?.favoritesSavedAtEpochMillis ?: 0,
                resumeItems = (resumeItems ?: current?.resumeItems.orEmpty()).map { it.toCacheCard() },
                favoriteItems = (favoriteItems ?: current?.favoriteItems.orEmpty()).map { it.toCacheCard() },
            )
            val encoded = adapter.toJson(snapshot)
            context.homeFeedCacheStore.edit { it[snapshotKey] = encoded }
        }
    }

    private suspend fun cacheScope(userId: String): String? {
        val session = sessionRepository.getHomeFeedCacheScope().first() ?: return null
        if (session.userId != userId) return null
        return homeFeedCacheScope(session.serverId, session.serverUrl, session.userId)
    }

    private fun MediaItem.toCacheCard() = HomeCardDto(
        id = id,
        name = name,
        type = type.name,
        overview = overview,
        year = year,
        premiereDate = premiereDate,
        genres = genres,
        imageTags = imageTags.mapKeys { it.key.name },
        backdropImageTags = backdropImageTags,
        seriesId = seriesId,
        seriesName = seriesName,
        seasonName = seasonName,
        indexNumber = indexNumber,
        parentIndexNumber = parentIndexNumber,
        playedPercentage = playedPercentage,
        playbackPositionTicks = playbackPositionTicks,
        primaryImageTag = primaryImageTag,
        seriesPrimaryImageTag = seriesPrimaryImageTag,
        seriesThumbImageTag = seriesThumbImageTag,
        parentThumbItemId = parentThumbItemId,
        parentThumbImageTag = parentThumbImageTag,
        parentBackdropItemId = parentBackdropItemId,
        parentBackdropImageTags = parentBackdropImageTags,
    )

    private fun HomeCardDto.toDomain() = MediaItem(
        id = id,
        name = name,
        type = runCatching { MediaItemType.valueOf(type) }.getOrDefault(MediaItemType.Unknown),
        overview = overview,
        year = year,
        premiereDate = premiereDate,
        genres = genres,
        imageTags = imageTags.mapNotNull { (key, value) ->
            runCatching { ImageType.valueOf(key) to value }.getOrNull()
        }.toMap(),
        backdropImageTags = backdropImageTags,
        seriesId = seriesId,
        seriesName = seriesName,
        seasonName = seasonName,
        indexNumber = indexNumber,
        parentIndexNumber = parentIndexNumber,
        playedPercentage = playedPercentage,
        playbackPositionTicks = playbackPositionTicks,
        primaryImageTag = primaryImageTag,
        seriesPrimaryImageTag = seriesPrimaryImageTag,
        seriesThumbImageTag = seriesThumbImageTag,
        parentThumbItemId = parentThumbItemId,
        parentThumbImageTag = parentThumbImageTag,
        parentBackdropItemId = parentBackdropItemId,
        parentBackdropImageTags = parentBackdropImageTags,
    )

    private companion object { const val SCHEMA_VERSION = 1 }
}

/** A stale read may only invalidate the exact value it observed, never a newer write. */
internal suspend fun removeHomeSnapshotIfUnchanged(
    dataStore: DataStore<Preferences>,
    key: Preferences.Key<String>,
    observedValue: String,
) {
    dataStore.edit { current ->
        if (current[key] == observedValue) current.remove(key)
    }
}

internal fun homeFeedCacheScope(serverId: String?, serverUrl: String, userId: String): String {
    val server = serverId?.trim()?.takeIf(String::isNotEmpty)?.let { "id:$it" }
        ?: "url:${normalizeServerUrl(serverUrl)}"
    val identity = "$server|user:$userId"
    return MessageDigest.getInstance("SHA-256")
        .digest(identity.toByteArray(Charsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
}

private fun normalizeServerUrl(value: String): String = runCatching {
    val uri = URI(value.trim().trimEnd('/'))
    "${uri.scheme?.lowercase()}://${uri.host?.lowercase()}:${uri.port}${uri.rawPath.orEmpty().trimEnd('/')}"
}.getOrDefault(value.trim().trimEnd('/').lowercase())
