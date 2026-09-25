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
import org.mulletaflix.core.api.dto.PlaylistCreationResultDto

class PlaylistRepositoryImplTest {
    private val api = mockk<MulletaFlixApiService>()
    private val repository = PlaylistRepositoryImpl(api)

    @Test
    fun getPlaylists_mapsServerItemsAndUsesPlaylistFilter() = runTest {
        coEvery { api.getPlaylists("user-1") } returns BaseItemDtoQueryResultDto(
            items = listOf(BaseItemDto(id = "pl-1", name = "Favoritos"))
        )

        val result = repository.getPlaylists("user-1")

        assertTrue(result.isSuccess)
        assertEquals(listOf(org.mulletaflix.domain.model.Playlist("pl-1", "Favoritos")), result.getOrThrow())
        coVerify(exactly = 1) { api.getPlaylists("user-1") }
    }

    @Test
    fun getPlaylistItems_mapsMediaAndPagingMetadata() = runTest {
        coEvery { api.getPlaylistItems("pl-1", "user-1", 20, 10, true, true) } returns BaseItemDtoQueryResultDto(
            items = listOf(BaseItemDto(id = "m-1", name = "Filme", type = "Movie")),
            totalRecordCount = 31,
            startIndex = 20,
        )

        val result = repository.getPlaylistItems("user-1", "pl-1", 20, 10).getOrThrow()

        assertEquals("m-1", result.first.single().id)
        assertEquals("Filme", result.first.single().name)
        assertEquals(31, result.second)
        coVerify(exactly = 1) { api.getPlaylistItems("pl-1", "user-1", 20, 10, true, true) }
    }

    @Test
    fun createPlaylist_returnsServerIdAndTrimsName() = runTest {
        coEvery { api.createPlaylist("Minha lista", "user-1", null) } returns PlaylistCreationResultDto("pl-2")

        val result = repository.createPlaylist("user-1", "  Minha lista  ")

        assertEquals(org.mulletaflix.domain.model.Playlist("pl-2", "Minha lista"), result.getOrThrow())
    }

    @Test
    fun addItem_sendsUserPlaylistAndItemIds() = runTest {
        coEvery { api.addItemToPlaylist("pl-1", "item-9", "user-1") } returns Unit

        val result = repository.addItem("user-1", "pl-1", "item-9")

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.addItemToPlaylist("pl-1", "item-9", "user-1") }
    }
}
