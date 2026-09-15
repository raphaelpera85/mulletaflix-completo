package org.mulletaflix.domain.model

/**
 * Pure Kotlin domain model representing a user's profile and permissions.
 */
data class UserProfile(
    val id: String,
    val name: String,
    val serverId: String? = null,
    val primaryImageTag: String? = null,
    val isAdministrator: Boolean = false,
    val canDownload: Boolean = true,
    val canAccessLiveTv: Boolean = true,
    val canPlayMedia: Boolean = true,
    val audioLanguagePreference: String? = null,
    val subtitleLanguagePreference: String? = null,
)
