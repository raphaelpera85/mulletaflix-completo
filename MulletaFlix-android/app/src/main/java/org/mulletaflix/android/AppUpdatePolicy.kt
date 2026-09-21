package org.mulletaflix.android

import org.mulletaflix.domain.model.AppUpdateInfo

/**
 * An update prompt is useful only when the release has a downloadable APK and
 * the user has not already dismissed that exact release in this session.
 */
internal fun shouldShowAppUpdateDialog(
    update: AppUpdateInfo,
    dismissedVersion: String?,
): Boolean = update.isUpdateAvailable &&
    !update.apkDownloadUrl.isNullOrBlank() &&
    update.latestVersion != dismissedVersion
