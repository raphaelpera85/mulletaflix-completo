package org.mulletaflix.domain.model

/** Stable account context captured when a media item is loaded. */
data class UserMediaPreferenceScope(
    val userId: String,
    val serverId: String?,
    val serverUrl: String,
)
