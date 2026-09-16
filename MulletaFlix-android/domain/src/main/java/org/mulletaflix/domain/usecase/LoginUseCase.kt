package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.UserSession
import javax.inject.Inject

/**
 * UseCase to authenticate user credentials against the media server.
 */
class LoginUseCase @Inject constructor(
    private val authRepository: AuthRepository,
) {
    suspend operator fun invoke(username: String, password: String): Result<UserSession> {
        val cleanUsername = username.trim()
        if (cleanUsername.isBlank()) {
            return Result.failure(IllegalArgumentException("O nome de usuário é obrigatório."))
        }
        return authRepository.login(cleanUsername, password)
    }
}
