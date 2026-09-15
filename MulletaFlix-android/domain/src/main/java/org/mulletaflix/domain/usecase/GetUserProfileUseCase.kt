package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import javax.inject.Inject

/**
 * UseCase to fetch the current authenticated user's profile and permissions.
 */
class GetUserProfileUseCase @Inject constructor(
    private val authRepository: AuthRepository,
) {
    suspend operator fun invoke(): Result<UserProfile> {
        return authRepository.getCurrentUserProfile()
    }
}
