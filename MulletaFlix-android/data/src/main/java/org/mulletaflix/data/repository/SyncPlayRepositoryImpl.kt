package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.JoinGroupRequestDto
import org.mulletaflix.core.api.dto.NewGroupRequestDto
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayRepository
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SyncPlayRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : SyncPlayRepository {

    override suspend fun getGroups(): Result<List<SyncPlayGroup>> = runCatching {
        api.getSyncPlayGroups().map {
            SyncPlayGroup(
                groupId = it.groupId,
                groupName = it.groupName,
                state = it.state,
                participants = it.participants,
                playingItemId = it.playingItemId,
                positionTicks = it.positionTicks,
            )
        }
    }

    override suspend fun createGroup(name: String): Result<Unit> = runCatching {
        api.createSyncPlayGroup(NewGroupRequestDto(groupName = name))
    }

    override suspend fun joinGroup(groupId: String): Result<Unit> = runCatching {
        api.joinSyncPlayGroup(JoinGroupRequestDto(groupId = groupId))
    }

    override suspend fun leaveGroup(): Result<Unit> = runCatching {
        api.leaveSyncPlayGroup()
    }
}
