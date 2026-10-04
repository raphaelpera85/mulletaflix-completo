package org.mulletaflix.data.repository

import kotlinx.coroutines.flow.first
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackIdentity
import org.mulletaflix.domain.repository.RemotePlaybackRepository
import org.mulletaflix.domain.repository.RemotePlaybackSession
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class RemotePlaybackRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
    private val sessionRepository: SessionRepository,
) : RemotePlaybackRepository {

    override suspend fun getActiveSessions(identity: RemotePlaybackIdentity): Result<List<RemotePlaybackSession>> = suspendRunCatching {
        val requestSession = requireCurrentSession(identity)
        api.getSessions(controllableByUserId = requestSession.userId, session = requestSession)
            .asSequence()
            .filter { !it.id.isNullOrBlank() && it.deviceId != requestSession.deviceId }
            .mapNotNull { session ->
                val playingItem = session.nowPlayingItem ?: return@mapNotNull null
                val id = session.id ?: return@mapNotNull null
                RemotePlaybackSession(
                    id = id,
                    deviceName = session.deviceName?.takeIf(String::isNotBlank) ?: session.client ?: "Dispositivo",
                    clientName = session.client?.takeIf(String::isNotBlank) ?: session.deviceName ?: "MulletaFlix",
                    itemName = playingItem.name?.takeIf(String::isNotBlank) ?: "Reproduzindo mídia",
                    isPaused = session.playState?.isPaused ?: false,
                    canSeek = session.playState?.canSeek ?: false,
                    positionTicks = session.playState?.positionTicks ?: 0L,
                    durationTicks = playingItem.runTimeTicks?.takeIf { it > 0L },
                )
            }
            .toList()
    }

    override suspend fun sendCommand(
        identity: RemotePlaybackIdentity,
        sessionId: String,
        command: RemotePlaybackCommand,
        seekPositionTicks: Long?,
    ): Result<Unit> = suspendRunCatching {
        require(sessionId.isNotBlank()) { "Sessão remota inválida." }
        val requestSession = requireCurrentSession(identity)
        val apiCommand = when (command) {
            RemotePlaybackCommand.PLAY_PAUSE -> "PlayPause"
            RemotePlaybackCommand.STOP -> "Stop"
            RemotePlaybackCommand.SEEK -> "Seek"
        }
        if (command == RemotePlaybackCommand.SEEK) {
            requireNotNull(seekPositionTicks) { "A posição para avançar é obrigatória." }
            require(seekPositionTicks >= 0L) { "A posição não pode ser negativa." }
        }
        api.sendSessionPlaystateCommand(
            sessionId = sessionId,
            command = apiCommand,
            seekPositionTicks = seekPositionTicks,
            controllingUserId = requestSession.userId,
            session = requestSession,
        )
    }

    private suspend fun requireCurrentSession(identity: RemotePlaybackIdentity): FeedbackRequestSession {
        val current = sessionRepository.getFeedbackRequestSession().first()
            ?: throw IllegalStateException("Sessão expirada. Entre novamente.")
        val currentIdentity = RemotePlaybackIdentity(current.serverId, current.serverUrl, current.userId)
        require(identity.matches(currentIdentity)) { "A sessão mudou. Atualize os dispositivos em reprodução." }
        return current
    }
}
