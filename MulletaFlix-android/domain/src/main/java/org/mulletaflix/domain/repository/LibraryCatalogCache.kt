package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.CachedLibraryCatalog
import org.mulletaflix.domain.model.MediaItem

/** Persistence contract for the most recently loaded, server-scoped library snapshot. */
interface LibraryCatalogCache {
    suspend fun read(userId: String, libraryId: String): CachedLibraryCatalog?

    suspend fun write(
        userId: String,
        libraryId: String,
        libraryName: String,
        collectionType: String?,
        sortBy: String,
        sortOrder: String,
        activeFilters: List<String>,
        items: List<MediaItem>,
        totalItemCount: Int,
    )
}

object NoOpLibraryCatalogCache : LibraryCatalogCache {
    override suspend fun read(userId: String, libraryId: String): CachedLibraryCatalog? = null

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
    ) = Unit
}
