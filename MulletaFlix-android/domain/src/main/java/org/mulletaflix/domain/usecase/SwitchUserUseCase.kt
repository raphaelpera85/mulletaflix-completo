package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.AuthRepository
import javax.inject.Inject

/**
 * UseCase to authenticate and switch to a different user account on the server.
 */
class SwitchUserUseCase @Inject constructor(
    private val authRepository: AuthRepository,
) {
    suspend operator fun invoke(username: String, password: String): Result<Unit> {
        val cleanUsername = username.trim()
        if (cleanUsername.isBlank()) {
            return Result.failure(IllegalArgumentException("O nome de usuário não pode estar vazio."))
        }
        return authRepository.login(cleanUsername, password).map { }
    }
}
