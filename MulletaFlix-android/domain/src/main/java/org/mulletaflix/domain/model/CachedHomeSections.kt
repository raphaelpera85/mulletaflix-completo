package org.mulletaflix.domain.model

/** Last server-confirmed snapshots of the two Home sections useful as offline references. */
data class CachedHomeSections(
    val resumeItems: List<MediaItem>,
    val favoriteItems: List<MediaItem>,
    val resumeSavedAtEpochMillis: Long,
    val favoritesSavedAtEpochMillis: Long,
)
