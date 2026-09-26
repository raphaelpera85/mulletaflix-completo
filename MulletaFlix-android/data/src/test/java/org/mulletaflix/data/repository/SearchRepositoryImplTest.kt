package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.BaseItemDtoQueryResultDto
import org.mulletaflix.core.api.dto.SearchHintDto
import org.mulletaflix.core.api.dto.SearchHintResultDto
import org.mulletaflix.domain.repository.SEARCH_PAGE_SIZE

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
                startIndex = 0,
                limit = SEARCH_PAGE_SIZE
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
        val items = result.getOrThrow().items
        assertEquals(1, items.size)
        assertEquals("av-1", items[0].id)
        assertEquals("Avengers: Endgame", items[0].name)
    }

    @Test
    fun searchItems_asksTheServerForTheRequestedPage() = runTest {
        coEvery {
            api.getItems(
                userId = "user-1",
                searchTerm = "a",
                includeItemTypes = null,
                startIndex = 30,
                limit = SEARCH_PAGE_SIZE
            )
        } returns BaseItemDtoQueryResultDto(
            items = (61..90).map { BaseItemDto(id = "it-$it", name = "Item $it", type = "Movie") },
            totalRecordCount = 412
        )

        val result = repository.searchItems("a", "user-1", null, startIndex = 30).getOrThrow()

        assertEquals("a segunda página começa no 61", "it-61", result.items.first().id)
        // O total viaja com cada página: sem ele a tela não saberia se ainda há mais.
        assertEquals(412, result.totalMatching)
        assertTrue(result.isTruncated)
    }

    @Test
    fun searchItems_keepsTheServerTotalSoTheScreenCanAdmitTruncation() = runTest {
        coEvery {
            api.getItems(
                userId = "user-1",
                searchTerm = "a",
                includeItemTypes = null,
                startIndex = 0,
                limit = SEARCH_PAGE_SIZE
            )
        } returns BaseItemDtoQueryResultDto(
            items = (1..30).map { BaseItemDto(id = "it-$it", name = "Item $it", type = "Movie") },
            totalRecordCount = 412
        )

        val result = repository.searchItems("a", "user-1", null).getOrThrow()

        assertEquals(30, result.items.size)
        assertEquals(412, result.totalMatching)
        assertTrue(result.isTruncated)
    }

    @Test
    fun searchItems_reportsNoTotalWhenTheServerCountContradictsThePage() = runTest {
        // `TotalRecordCount` é opcional no protocolo: quando falta, o DTO fica em 0.
        // Zero com 30 itens na mão não é "existem zero", é "o servidor não contou".
        coEvery {
            api.getItems(
                userId = "user-1",
                searchTerm = "a",
                includeItemTypes = null,
                startIndex = 0,
                limit = SEARCH_PAGE_SIZE
            )
        } returns BaseItemDtoQueryResultDto(
            items = (1..30).map { BaseItemDto(id = "it-$it", name = "Item $it", type = "Movie") },
            totalRecordCount = 0
        )

        val result = repository.searchItems("a", "user-1", null).getOrThrow()

        assertEquals(30, result.items.size)
        assertEquals(null, result.totalMatching)
        assertFalse(result.isTruncated)
    }

    @Test
    fun searchItems_treatsATotalBelowTheAlreadyPagedItemsAsUnknown() = runTest {
        // Numa página seguinte, o total tem de cobrir o que já veio antes: um servidor
        // que responde `TotalRecordCount = 5` depois de já termos 30 itens está se
        // contradizendo, e "não sei" é melhor do que afirmar 5.
        coEvery {
            api.getItems(
                userId = "user-1",
                searchTerm = "a",
                includeItemTypes = null,
                startIndex = 30,
                limit = SEARCH_PAGE_SIZE
            )
        } returns BaseItemDtoQueryResultDto(
            items = (31..60).map { BaseItemDto(id = "it-$it", name = "Item $it", type = "Movie") },
            totalRecordCount = 5
        )

        val result = repository.searchItems("a", "user-1", null, startIndex = 30).getOrThrow()

        assertEquals(30, result.items.size)
        assertEquals(null, result.totalMatching)
    }
}
