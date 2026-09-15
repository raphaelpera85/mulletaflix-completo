package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.AuthRepository
import javax.inject.Inject

/**
 * UseCase to terminate the current session and clear stored credentials.
 */
class LogoutUseCase @Inject constructor(
    private val authRepository: AuthRepository,
) {
    suspend operator fun invoke(): Result<Unit> {
        return authRepository.logout()
    }
}
