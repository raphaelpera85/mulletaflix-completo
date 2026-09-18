package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.AppUpdateInfo

/**
 * Contract for checking app update availability against release distribution channels.
 */
interface AppUpdateRepository {
    /**
     * Checks if a newer version of the Android app is available.
     *
     * @param currentVersion Semver string of the currently running app (e.g. "12.0.2").
     */
    suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo>
}
