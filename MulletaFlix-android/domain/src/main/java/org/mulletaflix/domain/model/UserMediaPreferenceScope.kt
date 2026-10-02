package org.mulletaflix.domain.model

/** Stable account context captured when a media item is loaded. */
data class UserMediaPreferenceScope(
    val userId: String,
    val serverId: String?,
    val serverUrl: String,
    /** Optional series identity for episode-specific track overrides. */
    val seriesId: String? = null,
    /** Distinguishes an episode with missing/invalid series metadata from a movie. */
    val isEpisode: Boolean = false,
)
