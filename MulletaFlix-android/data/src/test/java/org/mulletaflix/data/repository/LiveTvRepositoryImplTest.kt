package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.BaseItemDtoQueryResultDto
import org.mulletaflix.domain.model.MediaItem

class LiveTvRepositoryImplTest {

    private val api = mockk<MulletaFlixApiService>()
    private val repository = LiveTvRepositoryImpl(api)

    @Test
    fun getChannels_returnsMappedLiveTvChannels() = runTest {
        coEvery { api.getLiveTvChannels(userId = "user-1") } returns BaseItemDtoQueryResultDto(
            items = listOf(
                BaseItemDto(
                    id = "chan-1",
                    name = "HBO HD",
                    type = "TvChannel",
                    channelName = "501"
                )
            ),
            totalRecordCount = 1
        )

        val result = repository.getChannels("user-1")

        assertTrue(result.isSuccess)
        val channels = result.getOrThrow()
        assertEquals(1, channels.size)
        assertEquals("chan-1", channels[0].id)
        assertEquals("HBO HD", channels[0].name)
    }

    @Test
    fun scheduleRecording_failsWhenChannelIdIsMissing() = runTest {
        val invalidProgram = MediaItem(
            id = "prog-1",
            name = "Movie Special",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            channelId = null,
            startDate = "2026-09-16T20:00:00Z",
            endDate = "2026-09-16T22:00:00Z"
        )

        val result = repository.scheduleRecording(invalidProgram)

        assertTrue(result.isFailure)
        assertTrue(result.exceptionOrNull()?.message?.contains("canal válido") == true)
    }

    @Test
    fun scheduleRecording_succeedsWhenAllRequiredFieldsPresent() = runTest {
        val validProgram = MediaItem(
            id = "prog-1",
            name = "Live Football Match",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            overview = "Final championship game",
            channelId = "chan-10",
            startDate = "2026-09-16T20:00:00Z",
            endDate = "2026-09-16T22:00:00Z"
        )
        coEvery { api.createLiveTvTimer(any()) } returns Unit

        val result = repository.scheduleRecording(validProgram)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.createLiveTvTimer(any()) }
    }
}
