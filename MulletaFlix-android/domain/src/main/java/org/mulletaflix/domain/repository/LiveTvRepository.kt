package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.MediaItem

data class LiveTvChannel(
    val id: String,
    val name: String,
    val number: String?,
    val imageTag: String?,
    val currentProgram: String?,
)

interface LiveTvRepository {
    suspend fun getChannels(userId: String): Result<List<MediaItem>>
    suspend fun getPrograms(channelIds: List<String>, minStartDate: String?, maxEndDate: String?): Result<List<MediaItem>>
    suspend fun getRecordings(userId: String): Result<List<MediaItem>>
}
