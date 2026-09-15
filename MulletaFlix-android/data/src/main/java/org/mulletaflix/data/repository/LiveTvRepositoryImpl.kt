package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.CreateLiveTvTimerDto
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.LiveTvRepository
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class LiveTvRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : LiveTvRepository {

    override suspend fun getChannels(userId: String): Result<List<MediaItem>> = runCatching {
        api.getLiveTvChannels(userId = userId).items.map { it.toDomain() }
    }

    override suspend fun getPrograms(
        channelIds: List<String>,
        minStartDate: String?,
        maxEndDate: String?,
    ): Result<List<MediaItem>> = runCatching {
        api.getEpg(
            channelIds = channelIds.joinToString(","),
            minStartDate = minStartDate,
            maxEndDate = maxEndDate,
        ).items.map { it.toDomain() }
    }

    override suspend fun getRecordings(userId: String): Result<List<MediaItem>> = runCatching {
        api.getRecordings(userId = userId).items.map { it.toDomain() }
    }

    override suspend fun scheduleRecording(program: MediaItem): Result<Unit> = runCatching {
        val channelId = program.channelId?.takeIf(String::isNotBlank)
            ?: error("O programa não possui um canal válido.")
        val startDate = program.startDate?.takeIf(String::isNotBlank)
            ?: error("O programa não possui horário de início.")
        val endDate = program.endDate?.takeIf(String::isNotBlank)
            ?: error("O programa não possui horário de término.")
        api.createLiveTvTimer(
            CreateLiveTvTimerDto(
                programId = program.id,
                channelId = channelId,
                name = program.name,
                overview = program.overview,
                startDate = startDate,
                endDate = endDate,
            ),
        )
    }
}
