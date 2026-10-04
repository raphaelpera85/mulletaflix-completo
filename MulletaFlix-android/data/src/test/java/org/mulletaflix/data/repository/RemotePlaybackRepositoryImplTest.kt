package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.PlayerStateInfoDto
import org.mulletaflix.core.api.dto.SessionInfoDto
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackIdentity

class RemotePlaybackRepositoryImplTest {
    private val api = mockk<MulletaFlixApiService>()
    private val session = mockk<SessionRepository>()
    private val repository = RemotePlaybackRepositoryImpl(api, session)
    private val requestSession = org.mulletaflix.core.common.session.FeedbackRequestSession(
        "https://mulletaflix.example", "token-1", "user-1", "phone", "server-1",
    )
    private val identity = RemotePlaybackIdentity("server-1", "https://mulletaflix.example", "user-1")

    @Test
    fun `active sessions exclude this device and sessions not playing media`() = runTest {
        coEvery { session.getFeedbackRequestSession() } returns flowOf(requestSession)
        coEvery { api.getSessions("user-1", 300, requestSession) } returns listOf(
            SessionInfoDto(id = "self", deviceId = "phone", nowPlayingItem = BaseItemDto("a", "Self")),
            SessionInfoDto(id = "idle", deviceId = "tv", nowPlayingItem = null),
            SessionInfoDto(
                id = "remote",
                deviceId = "tv",
                deviceName = "Sala",
                client = "Android TV",
                nowPlayingItem = BaseItemDto("movie", "Filme em execução", runTimeTicks = 540_000_000L),
                playState = PlayerStateInfoDto(positionTicks = 50L, canSeek = true, isPaused = true),
            ),
        )

        val sessions = repository.getActiveSessions(identity).getOrThrow()

        assertEquals(1, sessions.size)
        assertEquals("remote", sessions.single().id)
        assertEquals("Sala", sessions.single().deviceName)
        assertEquals("Android TV", sessions.single().clientName)
        assertEquals("Filme em execução", sessions.single().itemName)
        assertTrue(sessions.single().isPaused)
        assertTrue(sessions.single().canSeek)
        assertEquals(50L, sessions.single().positionTicks)
        assertEquals(540_000_000L, sessions.single().durationTicks)
    }

    @Test
    fun `play pause command includes session and controlling user`() = runTest {
        coEvery { session.getFeedbackRequestSession() } returns flowOf(requestSession)
        coEvery { api.sendSessionPlaystateCommand("tv-session", "PlayPause", null, "user-1", requestSession) } returns Unit

        val result = repository.sendCommand(identity, "tv-session", RemotePlaybackCommand.PLAY_PAUSE)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) {
            api.sendSessionPlaystateCommand("tv-session", "PlayPause", null, "user-1", requestSession)
        }
    }

    @Test
    fun `seek rejects negative position before network request`() = runTest {
        coEvery { session.getFeedbackRequestSession() } returns flowOf(requestSession)

        val result = repository.sendCommand(identity, "tv-session", RemotePlaybackCommand.SEEK, -1L)

        assertTrue(result.isFailure)
        coVerify(exactly = 0) { api.sendSessionPlaystateCommand(any(), any(), any(), any(), any()) }
    }

    @Test
    fun `request is rejected before network when account or server changed`() = runTest {
        coEvery { session.getFeedbackRequestSession() } returns flowOf(requestSession.copy(userId = "user-2"))

        val result = repository.sendCommand(identity, "tv-session", RemotePlaybackCommand.STOP)

        assertTrue(result.isFailure)
        coVerify(exactly = 0) { api.sendSessionPlaystateCommand(any(), any(), any(), any(), any()) }
    }

    @Test
    fun `same server identity permits changing from public url to lan url`() = runTest {
        val lanSession = requestSession.copy(serverUrl = "http://192.168.1.20:8096")
        coEvery { session.getFeedbackRequestSession() } returns flowOf(lanSession)
        coEvery { api.getSessions("user-1", 300, lanSession) } returns emptyList()

        val result = repository.getActiveSessions(identity)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.getSessions("user-1", 300, lanSession) }
    }
}
