package org.mulletaflix.domain.model

/**
 * Represents update status and metadata for the MulletaFlix Android app.
 */
data class AppUpdateInfo(
    val isUpdateAvailable: Boolean,
    val currentVersion: String,
    val latestVersion: String,
    val releaseNotes: String? = null,
    val apkDownloadUrl: String? = null,
    val apkSize: Long = 0L,
    val publishedAt: String? = null,
)
