package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.dto.LiveStreamResponseDto
import org.mulletaflix.core.api.dto.MediaSourceDto
import org.mulletaflix.core.api.dto.PlaybackInfoRequestDto
import org.mulletaflix.core.api.dto.PlaybackInfoResponseDto
import org.mulletaflix.domain.repository.DEFAULT_PLAYBACK_PREPARATION_FAILURE

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

    /**
     * O servidor recusa preparar com HTTP 200 e a lista de fontes vazia, dizendo o
     * motivo em `ErrorCode`. O app desserializava o campo e nunca o lia, então as
     * três causas chegavam à tela como "nenhuma fonte disponível".
     */
    @Test
    fun `a recusa do servidor chega com o motivo que ele mandou`() = runTest {
        every { sessionRepository.getBaseUrl() } returns flowOf("http://server:8096")
        every { sessionRepository.getAccessToken() } returns flowOf("access-token")
        coEvery { api.getPlaybackInfo(any(), any(), any()) } returns PlaybackInfoResponseDto(
            playSessionId = null,
            mediaSources = emptyList(),
            errorCode = "RateLimitExceeded",
        )

        val info = repository.getPlaybackInfo(itemId = "item-1", userId = "user-1").getOrThrow()

        assertEquals(
            "o motivo do servidor precisa sobreviver até a tela",
            "Limite de transmissões simultâneas atingido. Encerre outra reprodução e tente de novo.",
            info.unavailableMessage,
        )
        assertNotEquals(
            "a frase genérica é justamente o que escondia a causa",
            DEFAULT_PLAYBACK_PREPARATION_FAILURE,
            info.unavailableMessage,
        )
    }

    @Test
    fun `uma recusa sem motivo fica com a frase generica`() = runTest {
        every { sessionRepository.getBaseUrl() } returns flowOf("http://server:8096")
        every { sessionRepository.getAccessToken() } returns flowOf("access-token")
        coEvery { api.getPlaybackInfo(any(), any(), any()) } returns PlaybackInfoResponseDto(
            playSessionId = null,
            mediaSources = emptyList(),
            errorCode = null,
        )

        val info = repository.getPlaybackInfo(itemId = "item-1", userId = "user-1").getOrThrow()

        assertNull(info.preparationError)
        assertEquals(DEFAULT_PLAYBACK_PREPARATION_FAILURE, info.unavailableMessage)
    }

    @Test
    fun `uma midia que o servidor preparou nao carrega erro de preparacao`() = runTest {
        every { sessionRepository.getBaseUrl() } returns flowOf("http://server:8096")
        every { sessionRepository.getAccessToken() } returns flowOf("access-token")
        coEvery { api.getPlaybackInfo(any(), any(), any()) } returns PlaybackInfoResponseDto(
            playSessionId = "session-1",
            mediaSources = listOf(MediaSourceDto(id = "source-1", container = "mkv")),
            errorCode = "RateLimitExceeded",
        )

        val info = repository.getPlaybackInfo(itemId = "item-1", userId = "user-1").getOrThrow()

        assertNull(
            "com fonte disponível o código de erro do servidor não diz respeito ao usuário",
            info.preparationError,
        )
        assertEquals(1, info.mediaSources.size)
    }

    /**
     * Um canal de tuner não é arquivo: o `PlaybackInfo` devolve a fonte com
     * `RequiresOpening = true` e sem `LiveStreamId`, e a rota de stream só sabe onde o
     * feed está depois de `LiveStreams/Open`. Sem esse passo o "Assistir" de um canal
     * desses nunca reproduzia.
     */
    @Test
    fun `a live channel is opened before its stream url is built`() = runTest {
        every { sessionRepository.getBaseUrl() } returns flowOf("http://server:8096")
        every { sessionRepository.getAccessToken() } returns flowOf("access-token")
        coEvery { api.getPlaybackInfo(any(), any(), any()) } returns PlaybackInfoResponseDto(
            playSessionId = "session-1",
            mediaSources = listOf(
                MediaSourceDto(
                    id = "source-1",
                    requiresOpening = true,
                    openToken = "open-token",
                ),
            ),
        )
        coEvery { api.openLiveStream(any(), any(), any(), any()) } returns LiveStreamResponseDto(
            mediaSource = MediaSourceDto(
                id = "source-opened",
                requiresOpening = false,
                liveStreamId = "live-1",
                container = "ts",
            ),
        )

        val info = repository.getPlaybackInfo(
            itemId = "channel-1",
            userId = "user-1",
            audioStreamIndex = null,
            subtitleStreamIndex = null,
            startTimeTicks = null,
        ).getOrThrow()

        coVerify(exactly = 1) {
            api.openLiveStream(
                userId = "user-1",
                itemId = "channel-1",
                playSessionId = "session-1",
                body = match { it.openToken == "open-token" },
            )
        }
        val source = info.mediaSources.single()
        assertTrue(
            "a URL do canal precisa carregar o LiveStreamId: ${source.directStreamUrl}",
            source.directStreamUrl.orEmpty().contains("LiveStreamId=live-1"),
        )
        assertTrue(
            "a URL de transcodificação também: ${source.transcodeUrl}",
            source.transcodeUrl.orEmpty().contains("LiveStreamId=live-1"),
        )
        assertTrue(
            "a fonte aberta é a que identifica o stream",
            source.directStreamUrl.orEmpty().contains("MediaSourceId=source-opened"),
        )
    }

    @Test
    fun `an ordinary movie is never opened as a live stream`() = runTest {
        every { sessionRepository.getBaseUrl() } returns flowOf("http://server:8096")
        every { sessionRepository.getAccessToken() } returns flowOf("access-token")
        coEvery { api.getPlaybackInfo(any(), any(), any()) } returns PlaybackInfoResponseDto(
            playSessionId = "session-1",
            mediaSources = listOf(MediaSourceDto(id = "source-1", container = "mkv")),
        )

        val info = repository.getPlaybackInfo(
            itemId = "movie-1",
            userId = "user-1",
            audioStreamIndex = null,
            subtitleStreamIndex = null,
            startTimeTicks = null,
        ).getOrThrow()

        coVerify(exactly = 0) { api.openLiveStream(any(), any(), any(), any()) }
        assertTrue(!info.mediaSources.single().directStreamUrl.orEmpty().contains("LiveStreamId"))
    }

    @Test
    fun `a channel opened by another session is reused instead of opened again`() = runTest {
        every { sessionRepository.getBaseUrl() } returns flowOf("http://server:8096")
        every { sessionRepository.getAccessToken() } returns flowOf("access-token")
        coEvery { api.getPlaybackInfo(any(), any(), any()) } returns PlaybackInfoResponseDto(
            playSessionId = "session-1",
            mediaSources = listOf(
                MediaSourceDto(
                    id = "source-1",
                    requiresOpening = true,
                    liveStreamId = "live-existing",
                ),
            ),
        )

        val info = repository.getPlaybackInfo(
            itemId = "channel-1",
            userId = "user-1",
            audioStreamIndex = null,
            subtitleStreamIndex = null,
            startTimeTicks = null,
        ).getOrThrow()

        coVerify(exactly = 0) { api.openLiveStream(any(), any(), any(), any()) }
        assertTrue(info.mediaSources.single().directStreamUrl.orEmpty().contains("LiveStreamId=live-existing"))
    }
}
