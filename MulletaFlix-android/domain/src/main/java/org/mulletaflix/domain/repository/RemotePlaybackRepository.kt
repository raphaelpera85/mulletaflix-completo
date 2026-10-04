package org.mulletaflix.domain.repository

import java.net.URI

data class RemotePlaybackIdentity(
    val serverId: String?,
    val serverUrl: String,
    val userId: String,
) {
    fun matches(other: RemotePlaybackIdentity): Boolean {
        if (userId != other.userId) return false

        val thisServerId = serverId?.trim()?.takeIf(String::isNotEmpty)
        val otherServerId = other.serverId?.trim()?.takeIf(String::isNotEmpty)
        if (thisServerId != null || otherServerId != null) {
            return thisServerId != null && otherServerId != null &&
                thisServerId.equals(otherServerId, ignoreCase = true)
        }

        return normalizeServerUrl(serverUrl) == normalizeServerUrl(other.serverUrl)
    }
}

private fun normalizeServerUrl(value: String): String = runCatching {
    val uri = URI(value.trim()).normalize()
    val scheme = uri.scheme?.lowercase().orEmpty()
    val host = uri.host?.lowercase().orEmpty()
    val port = uri.port.takeIf { it != -1 && !((scheme == "http" && it == 80) || (scheme == "https" && it == 443)) }
    val path = uri.path.orEmpty().trimEnd('/')
    "$scheme://$host${port?.let { ":$it" }.orEmpty()}$path"
}.getOrDefault(value.trim().trimEnd('/').lowercase())

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
    suspend fun getActiveSessions(identity: RemotePlaybackIdentity): Result<List<RemotePlaybackSession>>
    suspend fun sendCommand(
        identity: RemotePlaybackIdentity,
        sessionId: String,
        command: RemotePlaybackCommand,
        seekPositionTicks: Long? = null,
    ): Result<Unit>
}
