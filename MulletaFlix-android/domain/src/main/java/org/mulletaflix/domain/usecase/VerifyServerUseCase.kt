package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.ServerVerification
import javax.inject.Inject

/**
 * UseCase to verify server reachability, version and roundtrip latency.
 */
class VerifyServerUseCase @Inject constructor(
    private val authRepository: AuthRepository,
) {
    suspend operator fun invoke(url: String): Result<ServerVerification> {
        val cleanUrl = url.trim()
        if (cleanUrl.isBlank()) {
            return Result.failure(IllegalArgumentException("A URL do servidor não pode estar vazia."))
        }
        return authRepository.verifyServer(cleanUrl)
    }
}
