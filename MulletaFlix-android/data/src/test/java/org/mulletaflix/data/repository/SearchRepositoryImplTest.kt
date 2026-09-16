package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.BaseItemDtoQueryResultDto
import org.mulletaflix.core.api.dto.SearchHintDto
import org.mulletaflix.core.api.dto.SearchHintResultDto

class SearchRepositoryImplTest {

    private val api = mockk<MulletaFlixApiService>()
    private val repository = SearchRepositoryImpl(api)

    @Test
    fun searchHints_returnsMappedHintItems() = runTest {
        coEvery { api.searchHints("batman", "user-1") } returns SearchHintResultDto(
            searchHints = listOf(
                SearchHintDto(
                    itemId = "item-101",
                    name = "The Batman",
                    type = "Movie",
                    productionYear = 2022,
                    primaryImageTag = "imgTag123"
                )
            ),
            totalRecordCount = 1
        )

        val result = repository.searchHints("batman", "user-1")

        assertTrue(result.isSuccess)
        val hints = result.getOrThrow()
        assertEquals(1, hints.size)
        assertEquals("item-101", hints[0].id)
        assertEquals("The Batman", hints[0].name)
        assertEquals(2022, hints[0].year)
        assertEquals("imgTag123", hints[0].imageTag)
    }

    @Test
    fun searchHints_handlesNetworkFailure() = runTest {
        coEvery { api.searchHints(any(), any()) } throws RuntimeException("Connection refused")

        val result = repository.searchHints("matrix", "user-1")

        assertTrue(result.isFailure)
        assertEquals("Connection refused", result.exceptionOrNull()?.message)
    }

    @Test
    fun searchItems_returnsMappedDomainMediaItems() = runTest {
        coEvery {
            api.getItems(
                userId = "user-1",
                searchTerm = "avengers",
                includeItemTypes = "Movie",
                limit = 30
            )
        } returns BaseItemDtoQueryResultDto(
            items = listOf(
                BaseItemDto(
                    id = "av-1",
                    name = "Avengers: Endgame",
                    type = "Movie",
                    productionYear = 2019
                )
            ),
            totalRecordCount = 1
        )

        val result = repository.searchItems("avengers", "user-1", "Movie")

        assertTrue(result.isSuccess)
        val items = result.getOrThrow()
        assertEquals(1, items.size)
        assertEquals("av-1", items[0].id)
        assertEquals("Avengers: Endgame", items[0].name)
    }
}
