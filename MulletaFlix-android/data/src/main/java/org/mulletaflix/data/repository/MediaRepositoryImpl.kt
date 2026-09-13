package org.mulletaflix.data.repository

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.data.db.MediaItemDao
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.data.mapper.toEntity
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject
import javax.inject.Singleton

/**
 * MediaRepository implementation.
 *
 * Follows the offline-first strategy:
 *  1. Emit from Room cache immediately
 *  2. Fetch from network in parallel
 *  3. Update cache
 *  4. Emit updated data
 *
 * For one-shot reads (getItem, getSeasons, etc.) just hits network directly.
 */
@Singleton
class MediaRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
    private val dao: MediaItemDao,
) : MediaRepository {

    override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> =
        runCatching {
            api.getResumeItems(userId = userId, limit = limit)
                .items
                .map { it.toDomain() }
        }

    override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int): Result<List<MediaItem>> =
        runCatching {
            api.getLatestItems(userId = userId, parentId = parentId, limit = limit)
                .map { it.toDomain() }
        }

    override suspend fun getNextUp(userId: String, limit: Int): Result<List<MediaItem>> =
        runCatching {
            api.getNextUp(userId = userId, limit = limit)
                .items
                .map { it.toDomain() }
        }

    override suspend fun getLibraries(userId: String): Result<List<MediaItem>> =
        runCatching {
            api.getUserViews(userId = userId)
                .items
                .map { it.toDomain() }
        }

    override suspend fun getItems(
        userId: String,
        parentId: String?,
        includeItemTypes: String?,
        sortBy: String?,
        sortOrder: String?,
        filters: String?,
        searchTerm: String?,
        startIndex: Int,
        limit: Int,
        genres: String?,
        years: String?,
        isPlayed: Boolean?,
        isFavorite: Boolean?,
    ): Result<Pair<List<MediaItem>, Int>> = runCatching {
        val result = api.getItems(
            userId = userId,
            parentId = parentId,
            includeItemTypes = includeItemTypes,
            sortBy = sortBy,
            sortOrder = sortOrder ?: "Ascending",
            filters = filters,
            searchTerm = searchTerm,
            startIndex = startIndex,
            limit = limit,
            genres = genres,
            years = years,
            isPlayed = isPlayed,
            isFavorite = isFavorite,
        )
        Pair(result.items.map { it.toDomain() }, result.totalRecordCount)
    }

    override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> =
        runCatching { api.getItem(userId, itemId).toDomain() }

    override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int): Result<List<MediaItem>> =
        runCatching { api.getSimilarItems(itemId, userId, limit).items.map { it.toDomain() } }

    override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> =
        runCatching { api.getSeasons(seriesId, userId).items.map { it.toDomain() } }

    override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> =
        runCatching { api.getEpisodes(seriesId, userId, seasonId).items.map { it.toDomain() } }

    override suspend fun getSpecialFeatures(userId: String, itemId: String): Result<List<MediaItem>> =
        runCatching { api.getSpecialFeatures(itemId, userId).map { it.toDomain() } }

    override suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit> =
        runCatching { api.markAsPlayed(userId, itemId); Unit }

    override suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit> =
        runCatching { api.markAsUnplayed(userId, itemId); Unit }

    override suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit> =
        runCatching { api.markAsFavorite(userId, itemId); Unit }

    override suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit> =
        runCatching { api.unmarkAsFavorite(userId, itemId); Unit }

    override suspend fun search(userId: String, searchTerm: String, limit: Int, includeItemTypes: String?): Result<List<MediaItem>> =
        runCatching {
            api.getItems(
                userId = userId,
                searchTerm = searchTerm,
                limit = limit,
                includeItemTypes = includeItemTypes,
                recursive = true,
            ).items.map { it.toDomain() }
        }

    override suspend fun getLiveTvChannels(userId: String): Result<List<MediaItem>> =
        runCatching { api.getLiveTvChannels(userId = userId).items.map { it.toDomain() } }

    override suspend fun getRecordings(userId: String): Result<List<MediaItem>> =
        runCatching { api.getRecordings(userId = userId).items.map { it.toDomain() } }

    override suspend fun getSuggestions(userId: String, itemId: String): Result<List<MediaItem>> =
        runCatching { api.getSuggestions(itemId, userId).items.map { it.toDomain() } }

    override fun observeFavorites(userId: String): Flow<List<MediaItem>> =
        dao.observeFavorites(userId).map { entities -> entities.map { it.toDomain() } }

    override fun observeRecentlyWatched(userId: String): Flow<List<MediaItem>> =
        dao.observeRecentlyWatched(userId).map { entities -> entities.map { it.toDomain() } }
}
