package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayRepository
import javax.inject.Inject

/**
 * UseCase coordinating SyncPlay session operations (listing, creating, joining, leaving).
 */
class ManageSyncPlayUseCase @Inject constructor(
    private val syncPlayRepository: SyncPlayRepository,
) {
    suspend fun getGroups(): Result<List<SyncPlayGroup>> = syncPlayRepository.getGroups()

    suspend fun createGroup(name: String): Result<Unit> {
        val cleanName = name.trim()
        if (cleanName.isBlank()) {
            return Result.failure(IllegalArgumentException("O nome da sala não pode estar vazio."))
        }
        return syncPlayRepository.createGroup(cleanName)
    }

    suspend fun joinGroup(groupId: String): Result<Unit> {
        if (groupId.isBlank()) {
            return Result.failure(IllegalArgumentException("O identificador da sala não pode estar vazio."))
        }
        return syncPlayRepository.joinGroup(groupId)
    }

    suspend fun leaveGroup(): Result<Unit> = syncPlayRepository.leaveGroup()
}
