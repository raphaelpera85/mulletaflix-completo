package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow
import org.mulletaflix.domain.model.MediaItem

/**
 * Repository interface for media items — defined in domain, implemented in data.
 *
 * All methods return Result<T> to propagate network/db errors without exceptions
 * crossing layer boundaries.
 */
interface MediaRepository {

    // ── Home sections ────────────────────────────────────────────────────────

    suspend fun getResumeItems(userId: String, limit: Int = 12): Result<List<MediaItem>>

    suspend fun getLatestItems(userId: String, parentId: String? = null, limit: Int = 16): Result<List<MediaItem>>

    suspend fun getNextUp(userId: String, limit: Int = 12): Result<List<MediaItem>>

    // ── Library browsing ─────────────────────────────────────────────────────

    suspend fun getLibraries(userId: String): Result<List<MediaItem>>

    suspend fun getItems(
        userId: String,
        parentId: String? = null,
        includeItemTypes: String? = null,
        sortBy: String? = null,
        sortOrder: String? = null,
        filters: String? = null,
        searchTerm: String? = null,
        startIndex: Int = 0,
        limit: Int = 40,
        genres: String? = null,
        years: String? = null,
        isPlayed: Boolean? = null,
        isFavorite: Boolean? = null,
    ): Result<Pair<List<MediaItem>, Int>>   // items + total count

    // ── Item detail ──────────────────────────────────────────────────────────

    suspend fun getItem(userId: String, itemId: String): Result<MediaItem>

    suspend fun getSimilarItems(userId: String, itemId: String, limit: Int = 12): Result<List<MediaItem>>

    suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>>

    suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String? = null): Result<List<MediaItem>>

    suspend fun getSpecialFeatures(userId: String, itemId: String): Result<List<MediaItem>>

    // ── Playstate ────────────────────────────────────────────────────────────

    suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit>

    suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit>

    suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit>

    suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit>

    // ── Search ───────────────────────────────────────────────────────────────

    suspend fun search(
        userId: String,
        searchTerm: String,
        limit: Int = 20,
        includeItemTypes: String? = null,
    ): Result<List<MediaItem>>

    // ── Live TV ──────────────────────────────────────────────────────────────

    suspend fun getLiveTvChannels(userId: String): Result<List<MediaItem>>

    suspend fun getRecordings(userId: String): Result<List<MediaItem>>

    // ── Suggestions ──────────────────────────────────────────────────────────

    suspend fun getSuggestions(userId: String, itemId: String): Result<List<MediaItem>>

    // ── Reactive streams (Room local cache) ──────────────────────────────────

    fun observeFavorites(userId: String): Flow<List<MediaItem>>

    fun observeRecentlyWatched(userId: String): Flow<List<MediaItem>>
}
