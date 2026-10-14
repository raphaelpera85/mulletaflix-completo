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
    suspend operator fun invoke(currentVersion: String): Result<AppUpdateInfo> {
        val trimmed = currentVersion.trim()
        if (trimmed.isEmpty()) {
            return Result.failure(IllegalArgumentException("Versão atual não pode ser vazia."))
        }
        val cleanVersion = trimmed.removePrefix("v").removePrefix("V")
        if (cleanVersion.isEmpty()) {
            return Result.failure(IllegalArgumentException("Versão atual não pode ser vazia."))
        }
        return appUpdateRepository.checkForUpdate(cleanVersion)
    }
}
