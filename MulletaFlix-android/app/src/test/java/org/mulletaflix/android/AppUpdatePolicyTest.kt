package org.mulletaflix.android

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.android.update.TV_APP_UPDATE_CHECK_INTERVAL_MILLIS
import org.mulletaflix.android.update.appUpdateCheckIntervalMillis

class AppUpdatePolicyTest {
    @Test
    fun `periodic update checks are limited to televisions`() {
        assertTrue(appUpdateCheckIntervalMillis(isTelevision = true) == TV_APP_UPDATE_CHECK_INTERVAL_MILLIS)
        assertTrue(appUpdateCheckIntervalMillis(isTelevision = false) == 0L)
        assertTrue(TV_APP_UPDATE_CHECK_INTERVAL_MILLIS >= 60L * 60L * 1000L)
    }

    private fun update(
        available: Boolean = true,
        downloadUrl: String? = "https://example.test/app.apk",
        latestVersion: String = "1.2.28",
    ) = AppUpdateInfo(
        isUpdateAvailable = available,
        currentVersion = "1.2.27",
        latestVersion = latestVersion,
        apkDownloadUrl = downloadUrl,
    )

    @Test
    fun `shows a downloadable update when it was not dismissed`() {
        assertTrue(shouldShowAppUpdateDialog(update(), dismissedVersion = null))
    }

    @Test
    fun `does not show the same dismissed release again`() {
        assertFalse(shouldShowAppUpdateDialog(update(), dismissedVersion = "1.2.28"))
    }

    @Test
    fun `does not show unavailable or non downloadable update`() {
        assertFalse(shouldShowAppUpdateDialog(update(available = false), dismissedVersion = null))
        assertFalse(shouldShowAppUpdateDialog(update(downloadUrl = null), dismissedVersion = null))
    }
}
