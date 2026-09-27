package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.CachedHomeSections
import org.mulletaflix.domain.model.MediaItem

/** Persistence contract for user- and server-scoped Home snapshots. */
interface HomeFeedCache {
    suspend fun read(userId: String): CachedHomeSections?

    /** Null section means preserve its previous server-confirmed snapshot. */
    suspend fun write(
        userId: String,
        resumeItems: List<MediaItem>?,
        favoriteItems: List<MediaItem>?,
    )
}

object NoOpHomeFeedCache : HomeFeedCache {
    override suspend fun read(userId: String): CachedHomeSections? = null
    override suspend fun write(userId: String, resumeItems: List<MediaItem>?, favoriteItems: List<MediaItem>?) = Unit
}
