package org.mulletaflix.domain.repository

data class RemotePlaybackSession(
    val id: String,
    val deviceName: String,
    val clientName: String,
    val itemName: String,
    val isPaused: Boolean,
    val canSeek: Boolean,
    val positionTicks: Long,
    val durationTicks: Long? = null,
)

enum class RemotePlaybackCommand { PLAY_PAUSE, STOP, SEEK }

interface RemotePlaybackRepository {
    suspend fun getActiveSessions(): Result<List<RemotePlaybackSession>>
    suspend fun sendCommand(
        sessionId: String,
        command: RemotePlaybackCommand,
        seekPositionTicks: Long? = null,
    ): Result<Unit>
}
