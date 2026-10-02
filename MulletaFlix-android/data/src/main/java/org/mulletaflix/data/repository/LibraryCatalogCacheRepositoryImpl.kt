package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.squareup.moshi.JsonClass
import com.squareup.moshi.Moshi
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.CachedLibraryCatalog
import org.mulletaflix.domain.model.ImageType
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.LibraryCatalogCache
import java.net.URI
import java.security.MessageDigest
import javax.inject.Inject
import javax.inject.Singleton

private val Context.libraryCatalogCacheStore by preferencesDataStore(name = "mulletaflix_library_cache")

/** A strict card-only DTO: no media sources, streams, URLs, file paths or credentials. */
@JsonClass(generateAdapter = true)
internal data class LibraryCatalogSnapshotDto(
    val schemaVersion: Int = 1,
    val scope: String,
    val libraryId: String,
    val libraryName: String,
    val collectionType: String? = null,
    val sortBy: String,
    val sortOrder: String,
    val activeFilters: List<String>,
    val savedAtEpochMillis: Long,
    val totalItemCount: Int,
    val items: List<LibraryCardSnapshotDto>,
)

@JsonClass(generateAdapter = true)
internal data class LibraryCatalogStoreDto(
    val schemaVersion: Int = 1,
    val snapshots: List<LibraryCatalogSnapshotDto>,
)

@JsonClass(generateAdapter = true)
internal data class LibraryCardSnapshotDto(
    val id: String,
    val name: String,
    val type: String,
    val overview: String? = null,
    val year: Int? = null,
    val premiereDate: String? = null,
    val imageTags: Map<String, String> = emptyMap(),
    val backdropImageTags: List<String> = emptyList(),
    val seriesId: String? = null,
    val seriesName: String? = null,
    val seasonName: String? = null,
    val indexNumber: Int? = null,
    val parentIndexNumber: Int? = null,
    val playedPercentage: Double? = null,
    val unplayedItemCount: Int? = null,
    val isFavorite: Boolean = false,
    val isPlayed: Boolean = false,
    val has4K: Boolean = false,
    val hasHD: Boolean = false,
    val primaryImageTag: String? = null,
    val seriesPrimaryImageTag: String? = null,
    val seriesThumbImageTag: String? = null,
    val parentThumbItemId: String? = null,
    val parentThumbImageTag: String? = null,
    val parentBackdropItemId: String? = null,
    val parentBackdropImageTags: List<String> = emptyList(),
)

@Singleton
class LibraryCatalogCacheRepositoryImpl @Inject constructor(
    @param:ApplicationContext private val context: Context,
    private val sessionRepository: SessionRepository,
) : LibraryCatalogCache {
    private val key = stringPreferencesKey("snapshots_v1")
    private val adapter = Moshi.Builder().build().adapter(LibraryCatalogStoreDto::class.java)
    private val writeMutex = Mutex()

    override suspend fun read(userId: String, libraryId: String): CachedLibraryCatalog? {
        val scope = cacheScope(userId) ?: return null
        val preferences = context.libraryCatalogCacheStore.data.first()
        val encoded = preferences[key] ?: return null
        val store = runCatching { adapter.fromJson(encoded) }.getOrNull()
        if (store == null || store.schemaVersion != SCHEMA_VERSION) {
            removeLibrarySnapshotIfUnchanged(context.libraryCatalogCacheStore, key, encoded)
            return null
        }
        val snapshot = store.snapshots.lastOrNull { it.scope == scope && it.libraryId == libraryId } ?: return null
        return CachedLibraryCatalog(
            libraryId = snapshot.libraryId,
            libraryName = snapshot.libraryName,
            collectionType = snapshot.collectionType,
            items = snapshot.items.map { it.toDomain() },
            sortBy = snapshot.sortBy,
            sortOrder = snapshot.sortOrder,
            activeFilters = snapshot.activeFilters,
            savedAtEpochMillis = snapshot.savedAtEpochMillis,
            totalItemCount = snapshot.totalItemCount,
        )
    }

    override suspend fun write(
        userId: String,
        libraryId: String,
        libraryName: String,
        collectionType: String?,
        sortBy: String,
        sortOrder: String,
        activeFilters: List<String>,
        items: List<MediaItem>,
        totalItemCount: Int,
    ) {
        writeMutex.withLock {
            val scope = cacheScope(userId) ?: return
            if (!libraryId.isSafeCacheIdentifier()) return
            val snapshot = LibraryCatalogSnapshotDto(
                scope = scope,
                libraryId = libraryId,
                libraryName = libraryName.safeText(MAX_TITLE_LENGTH),
                collectionType = collectionType?.safeText(MAX_TITLE_LENGTH),
                sortBy = sortBy.take(MAX_TITLE_LENGTH),
                sortOrder = sortOrder.take(MAX_TITLE_LENGTH),
                activeFilters = activeFilters.take(MAX_FILTERS).map { it.safeText(MAX_TITLE_LENGTH) },
                savedAtEpochMillis = System.currentTimeMillis(),
                totalItemCount = totalItemCount.coerceAtLeast(items.size),
                items = librarySnapshotItems(items).map { it.toSnapshotCard() },
            )
            val current = context.libraryCatalogCacheStore.data.first()[key]
            val existing = current?.let { runCatching { adapter.fromJson(it) }.getOrNull() }
                ?.takeIf { it.schemaVersion == SCHEMA_VERSION }
                ?.snapshots
                .orEmpty()
                .filterNot { it.libraryId == libraryId && it.scope == scope }
            val boundedStore = LibraryCatalogStoreDto(
                snapshots = (existing.filter { it.scope == scope } + snapshot)
                    .sortedByDescending { it.savedAtEpochMillis }
                    .take(MAX_CACHED_LIBRARIES),
            )
            context.libraryCatalogCacheStore.edit { it[key] = adapter.toJson(boundedStore) }
        }
    }

    private suspend fun cacheScope(userId: String): String? {
        val session = sessionRepository.getHomeFeedCacheScope().first() ?: return null
        if (session.userId != userId) return null
        return libraryCatalogCacheScope(session.serverId, session.serverUrl, session.userId)
    }

    private fun MediaItem.toSnapshotCard() = LibraryCardSnapshotDto(
        id = id,
        name = name.safeText(MAX_TITLE_LENGTH),
        type = type.name,
        overview = overview?.safeText(MAX_OVERVIEW_LENGTH),
        year = year,
        premiereDate = premiereDate?.take(MAX_TITLE_LENGTH),
        imageTags = imageTags.entries.take(MAX_IMAGE_TAGS).mapNotNull { (type, tag) ->
            if (tag.isSafeCacheIdentifier()) type.name to tag else null
        }.toMap(),
        backdropImageTags = backdropImageTags.filter(String::isSafeCacheIdentifier).take(MAX_IMAGE_TAGS),
        seriesId = seriesId?.takeIf(String::isSafeCacheIdentifier),
        seriesName = seriesName?.safeText(MAX_TITLE_LENGTH),
        seasonName = seasonName?.safeText(MAX_TITLE_LENGTH),
        indexNumber = indexNumber,
        parentIndexNumber = parentIndexNumber,
        playedPercentage = playedPercentage?.takeIf(Double::isFinite)?.coerceIn(0.0, 100.0),
        unplayedItemCount = unplayedItemCount?.coerceAtLeast(0),
        isFavorite = isFavorite,
        isPlayed = isPlayed,
        has4K = has4K,
        hasHD = hasHD,
        primaryImageTag = primaryImageTag?.takeIf(String::isSafeCacheIdentifier),
        seriesPrimaryImageTag = seriesPrimaryImageTag?.takeIf(String::isSafeCacheIdentifier),
        seriesThumbImageTag = seriesThumbImageTag?.takeIf(String::isSafeCacheIdentifier),
        parentThumbItemId = parentThumbItemId?.takeIf(String::isSafeCacheIdentifier),
        parentThumbImageTag = parentThumbImageTag?.takeIf(String::isSafeCacheIdentifier),
        parentBackdropItemId = parentBackdropItemId?.takeIf(String::isSafeCacheIdentifier),
        parentBackdropImageTags = parentBackdropImageTags.filter(String::isSafeCacheIdentifier).take(MAX_IMAGE_TAGS),
    )

    private fun LibraryCardSnapshotDto.toDomain() = MediaItem(
        id = id,
        name = name,
        type = runCatching { MediaItemType.valueOf(type) }.getOrDefault(MediaItemType.Unknown),
        overview = overview,
        year = year,
        premiereDate = premiereDate,
        imageTags = imageTags.mapNotNull { (name, tag) ->
            runCatching { ImageType.valueOf(name) to tag }.getOrNull()
        }.toMap(),
        backdropImageTags = backdropImageTags,
        seriesId = seriesId,
        seriesName = seriesName,
        seasonName = seasonName,
        indexNumber = indexNumber,
        parentIndexNumber = parentIndexNumber,
        playedPercentage = playedPercentage,
        unplayedItemCount = unplayedItemCount,
        isFavorite = isFavorite,
        isPlayed = isPlayed,
        has4K = has4K,
        hasHD = hasHD,
        primaryImageTag = primaryImageTag,
        seriesPrimaryImageTag = seriesPrimaryImageTag,
        seriesThumbImageTag = seriesThumbImageTag,
        parentThumbItemId = parentThumbItemId,
        parentThumbImageTag = parentThumbImageTag,
        parentBackdropItemId = parentBackdropItemId,
        parentBackdropImageTags = parentBackdropImageTags,
    )

    private companion object {
        const val SCHEMA_VERSION = 1
        const val MAX_TITLE_LENGTH = 300
        const val MAX_OVERVIEW_LENGTH = 600
        const val MAX_ID_LENGTH = 128
        const val MAX_IMAGE_TAGS = 8
        const val MAX_FILTERS = 12
        const val MAX_CACHED_LIBRARIES = 8
    }
}

internal fun libraryCatalogCacheScope(serverId: String?, serverUrl: String, userId: String): String {
    val server = serverId?.trim()?.takeIf(String::isNotEmpty)?.let { "id:$it" }
        ?: "url:${runCatching {
            val uri = URI(serverUrl.trim().trimEnd('/'))
            "${uri.scheme?.lowercase()}://${uri.host?.lowercase()}:${uri.port}${uri.rawPath.orEmpty().trimEnd('/')}"
        }.getOrDefault(serverUrl.trim().trimEnd('/').lowercase())}"
    return MessageDigest.getInstance("SHA-256")
        .digest("$server|user:$userId".toByteArray(Charsets.UTF_8))
        .joinToString("") { "%02x".format(it) }
}

internal const val MAX_LIBRARY_CACHED_ITEMS = 200

internal fun librarySnapshotItems(items: List<MediaItem>): List<MediaItem> =
    items.asSequence().filter { it.id.isSafeCacheIdentifier() }.take(MAX_LIBRARY_CACHED_ITEMS).toList()

private fun String.isSafeCacheIdentifier(): Boolean =
    length in 1..128 && all { it in 'a'..'z' || it in 'A'..'Z' || it in '0'..'9' || it == '-' || it == '_' }

private fun String.safeText(maxLength: Int): String = take(maxLength).filterNot { it == '\u0000' }

/** A stale reader can remove only the exact snapshot it observed. */
internal suspend fun removeLibrarySnapshotIfUnchanged(
    dataStore: androidx.datastore.core.DataStore<androidx.datastore.preferences.core.Preferences>,
    key: androidx.datastore.preferences.core.Preferences.Key<String>,
    observedValue: String,
) {
    dataStore.edit { current -> if (current[key] == observedValue) current.remove(key) }
}
