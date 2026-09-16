package org.mulletaflix.domain.model

/**
 * Domain model representing aggregated sections for the home screen.
 */
data class HomeFeed(
    val heroItem: MediaItem? = null,
    val resumeItems: List<MediaItem> = emptyList(),
    val nextUpItems: List<MediaItem> = emptyList(),
    val recentlyAddedByLibrary: Map<String, List<MediaItem>> = emptyMap(),
    val liveTvChannels: List<MediaItem> = emptyList(),
    val libraries: List<MediaItem> = emptyList(),
)
