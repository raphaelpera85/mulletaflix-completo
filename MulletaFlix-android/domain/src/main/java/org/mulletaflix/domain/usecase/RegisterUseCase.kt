package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.RegistrationResult
import javax.inject.Inject

/**
 * UseCase to register a new user account on the media server.
 */
class RegisterUseCase @Inject constructor(
    private val authRepository: AuthRepository,
) {
    suspend operator fun invoke(username: String, password: String): Result<RegistrationResult> {
        val cleanUsername = username.trim().lowercase()
        if (cleanUsername.isBlank()) {
            return Result.failure(IllegalArgumentException("Digite um nome de usuário ou e-mail."))
        }
        if (password.length < 8) {
            return Result.failure(IllegalArgumentException("A senha deve ter pelo menos 8 caracteres."))
        }
        return authRepository.register(cleanUsername, password)
    }
}
