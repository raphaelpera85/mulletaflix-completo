package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.JoinGroupRequestDto
import org.mulletaflix.core.api.dto.NewGroupRequestDto
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayRepository
import org.mulletaflix.domain.repository.SyncPlayPlaybackCommand
import org.mulletaflix.domain.repository.SyncPlayPlaybackStatus
import org.mulletaflix.core.api.dto.SyncPlayPlaybackStatusDto
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SyncPlayRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : SyncPlayRepository {

    override suspend fun getGroups(): Result<List<SyncPlayGroup>> = suspendRunCatching {
        api.getSyncPlayGroups().map {
            SyncPlayGroup(
                groupId = it.groupId,
                groupName = it.groupName,
                state = it.state,
                participants = it.participants,
            )
        }
    }

    override suspend fun createGroup(name: String): Result<Unit> = suspendRunCatching {
        api.createSyncPlayGroup(NewGroupRequestDto(groupName = name))
    }

    override suspend fun joinGroup(groupId: String): Result<Unit> = suspendRunCatching {
        api.joinSyncPlayGroup(JoinGroupRequestDto(groupId = groupId))
    }

    override suspend fun leaveGroup(): Result<Unit> = suspendRunCatching {
        api.leaveSyncPlayGroup()
    }

    override suspend fun sendPlaybackCommand(command: SyncPlayPlaybackCommand): Result<Unit> = suspendRunCatching {
        when (command) {
            SyncPlayPlaybackCommand.PAUSE -> api.pauseSyncPlay()
            SyncPlayPlaybackCommand.UNPAUSE -> api.unpauseSyncPlay()
            SyncPlayPlaybackCommand.STOP -> api.stopSyncPlay()
        }
    }

    override suspend fun reportBuffering(status: SyncPlayPlaybackStatus): Result<Unit> = suspendRunCatching {
        api.reportSyncPlayBuffering(status.toDto())
    }

    override suspend fun reportReady(status: SyncPlayPlaybackStatus): Result<Unit> = suspendRunCatching {
        api.reportSyncPlayReady(status.toDto())
    }

    private fun SyncPlayPlaybackStatus.toDto() = SyncPlayPlaybackStatusDto(
        whenUtc = whenUtc,
        positionTicks = positionTicks,
        isPlaying = isPlaying,
        playlistItemId = playlistItemId,
    )
}
