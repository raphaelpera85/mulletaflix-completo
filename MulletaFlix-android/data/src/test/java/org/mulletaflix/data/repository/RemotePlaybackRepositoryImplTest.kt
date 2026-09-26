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

class RemotePlaybackRepositoryImplTest {
    private val api = mockk<MulletaFlixApiService>()
    private val session = mockk<SessionRepository>()
    private val repository = RemotePlaybackRepositoryImpl(api, session)

    @Test
    fun `active sessions exclude this device and sessions not playing media`() = runTest {
        coEvery { session.getCurrentUserId() } returns flowOf("user-1")
        coEvery { session.getDeviceId() } returns flowOf("phone")
        coEvery { api.getSessions("user-1", 300) } returns listOf(
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

        val sessions = repository.getActiveSessions().getOrThrow()

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
        coEvery { session.getCurrentUserId() } returns flowOf("user-1")
        coEvery { api.sendSessionPlaystateCommand("tv-session", "PlayPause", null, "user-1") } returns Unit

        val result = repository.sendCommand("tv-session", RemotePlaybackCommand.PLAY_PAUSE)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) {
            api.sendSessionPlaystateCommand("tv-session", "PlayPause", null, "user-1")
        }
    }

    @Test
    fun `seek rejects negative position before network request`() = runTest {
        coEvery { session.getCurrentUserId() } returns flowOf("user-1")

        val result = repository.sendCommand("tv-session", RemotePlaybackCommand.SEEK, -1L)

        assertTrue(result.isFailure)
        coVerify(exactly = 0) { api.sendSessionPlaystateCommand(any(), any(), any(), any()) }
    }
}
