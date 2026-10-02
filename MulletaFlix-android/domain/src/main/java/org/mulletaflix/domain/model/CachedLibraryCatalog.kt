package org.mulletaflix.domain.model

/** Last server-confirmed library result, stored only as a read-only offline reference. */
data class CachedLibraryCatalog(
    val libraryId: String,
    val libraryName: String,
    val collectionType: String?,
    val items: List<MediaItem>,
    val totalItemCount: Int,
    val sortBy: String,
    val sortOrder: String,
    val activeFilters: List<String>,
    val savedAtEpochMillis: Long,
)
