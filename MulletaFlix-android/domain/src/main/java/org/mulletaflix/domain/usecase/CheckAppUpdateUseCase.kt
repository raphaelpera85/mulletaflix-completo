package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.repository.AppUpdateRepository
import javax.inject.Inject

/**
 * UseCase to check if an app update is available for the current installed version.
 */
class CheckAppUpdateUseCase @Inject constructor(
    private val appUpdateRepository: AppUpdateRepository,
) {
    suspend operator fun invoke(currentVersion: String): Result<AppUpdateInfo> =
        appUpdateRepository.checkForUpdate(currentVersion)
}
