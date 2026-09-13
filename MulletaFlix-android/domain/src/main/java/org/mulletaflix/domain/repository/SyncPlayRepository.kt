package org.mulletaflix.domain.repository

data class SyncPlayGroup(
    val groupId: String,
    val groupName: String,
    val state: String?,
    val participants: List<String>,
    val playingItemId: String?,
    val positionTicks: Long,
)

interface SyncPlayRepository {
    suspend fun getGroups(): Result<List<SyncPlayGroup>>
    suspend fun createGroup(name: String): Result<Unit>
    suspend fun joinGroup(groupId: String): Result<Unit>
    suspend fun leaveGroup(): Result<Unit>
}
