package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.MediaSourceDto
import org.mulletaflix.core.api.dto.PlaybackInfoRequestDto
import org.mulletaflix.core.api.dto.PlaybackInfoResponseDto

class PlaybackRepositoryImplTest {
    private val api = mockk<MulletaFlixApiService>()
    private val sessionRepository = mockk<SessionRepository>()
    private val repository = PlaybackRepositoryImpl(api, sessionRepository)

    @Test
    fun `getPlaybackInfo sends selected tracks and resume position to server`() = runTest {
        every { sessionRepository.getBaseUrl() } returns flowOf("http://server:8096/")
        every { sessionRepository.getAccessToken() } returns flowOf("access-token")
        val expectedRequest = PlaybackInfoRequestDto(
            userId = "user-1",
            audioStreamIndex = 5,
            subtitleStreamIndex = 9,
            startTimeTicks = 42L,
            enableDirectPlay = true,
            enableDirectStream = true,
            enableTranscoding = true,
        )
        coEvery {
            api.getPlaybackInfo(
                itemId = "item-1",
                userId = "user-1",
                body = expectedRequest,
            )
        } returns PlaybackInfoResponseDto(
            playSessionId = "session-1",
            mediaSources = listOf(MediaSourceDto(id = "source-1", container = "mkv")),
        )

        val result = repository.getPlaybackInfo(
            itemId = "item-1",
            userId = "user-1",
            audioStreamIndex = 5,
            subtitleStreamIndex = 9,
            startTimeTicks = 42L,
        )

        assertTrue(result.isSuccess)
        assertEquals("session-1", result.getOrThrow().playSessionId)
        coVerify(exactly = 1) {
            api.getPlaybackInfo(
                itemId = "item-1",
                userId = "user-1",
                body = expectedRequest,
            )
        }
    }
}
