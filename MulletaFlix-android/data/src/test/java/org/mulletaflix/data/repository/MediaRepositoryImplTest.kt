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
import org.mulletaflix.core.api.dto.UserItemDataDto

class MediaRepositoryImplTest {

    private val api = mockk<MulletaFlixApiService>()
    private val repository = MediaRepositoryImpl(api)

    private val sampleDto = BaseItemDto(
        id = "movie-1",
        name = "Inception",
        type = "Movie",
        overview = "A mind-bending thriller",
        productionYear = 2010,
    )

    @Test
    fun getResumeItems_returnsMappedDomainList() = runTest {
        coEvery { api.getResumeItems(userId = "user-1", limit = 12) } returns BaseItemDtoQueryResultDto(
            items = listOf(sampleDto),
            totalRecordCount = 1
        )

        val result = repository.getResumeItems("user-1", limit = 12)

        assertTrue(result.isSuccess)
        val items = result.getOrThrow()
        assertEquals(1, items.size)
        assertEquals("movie-1", items[0].id)
        assertEquals("Inception", items[0].name)
    }

    @Test
    fun getResumeItems_handlesApiFailure() = runTest {
        coEvery { api.getResumeItems(any(), any()) } throws RuntimeException("Network timeout")

        val result = repository.getResumeItems("user-1", limit = 10)

        assertTrue(result.isFailure)
        assertEquals("Network timeout", result.exceptionOrNull()?.message)
    }

    @Test
    fun getLatestItems_returnsMappedDomainList() = runTest {
        coEvery { api.getLatestItems("user-1", parentId = "lib-1", limit = 16) } returns listOf(sampleDto)

        val result = repository.getLatestItems("user-1", parentId = "lib-1", limit = 16)

        assertTrue(result.isSuccess)
        assertEquals(1, result.getOrThrow().size)
        assertEquals("Inception", result.getOrThrow()[0].name)
    }

    @Test
    fun getNextUp_returnsMappedDomainList() = runTest {
        coEvery { api.getNextUp(userId = "user-1", limit = 5) } returns BaseItemDtoQueryResultDto(
            items = listOf(sampleDto),
            totalRecordCount = 1
        )

        val result = repository.getNextUp("user-1", limit = 5)

        assertTrue(result.isSuccess)
        assertEquals("movie-1", result.getOrThrow().first().id)
    }

    @Test
    fun getLibraries_returnsMappedDomainList() = runTest {
        coEvery { api.getUserViews(userId = "user-1") } returns BaseItemDtoQueryResultDto(
            items = listOf(sampleDto.copy(id = "view-1", name = "Filmes", type = "CollectionFolder")),
            totalRecordCount = 1
        )

        val result = repository.getLibraries("user-1")

        assertTrue(result.isSuccess)
        assertEquals("Filmes", result.getOrThrow().first().name)
    }

    @Test
    fun getItems_returnsPairWithItemsAndTotalCount() = runTest {
        coEvery {
            api.getItems(
                userId = "user-1",
                parentId = "folder-1",
                includeItemTypes = "Movie",
                sortBy = "SortName",
                sortOrder = "Ascending",
                filters = null,
                searchTerm = null,
                startIndex = 0,
                limit = 20,
                genres = null,
                years = null,
                officialRatings = null,
                isPlayed = null,
                isFavorite = null
            )
        } returns BaseItemDtoQueryResultDto(items = listOf(sampleDto), totalRecordCount = 42)

        val result = repository.getItems(
            userId = "user-1",
            parentId = "folder-1",
            includeItemTypes = "Movie",
            sortBy = "SortName",
            sortOrder = "Ascending",
            filters = null,
            searchTerm = null,
            startIndex = 0,
            limit = 20,
            genres = null,
            years = null,
            officialRatings = null,
            isPlayed = null,
            isFavorite = null
        )

        assertTrue(result.isSuccess)
        val (items, total) = result.getOrThrow()
        assertEquals(1, items.size)
        assertEquals(42, total)
    }

    @Test
    fun getItems_forwardsGenresYearsAndOfficialRatings() = runTest {
        coEvery {
            api.getItems(
                userId = "user-1",
                parentId = "folder-1",
                includeItemTypes = "Movie",
                sortBy = "SortName",
                sortOrder = "Ascending",
                filters = null,
                searchTerm = null,
                startIndex = 0,
                limit = 20,
                genres = "Drama|Ação",
                years = "2023,2024",
                officialRatings = "PG-13|TV-MA",
                isPlayed = true,
                isFavorite = true,
            )
        } returns BaseItemDtoQueryResultDto(items = listOf(sampleDto), totalRecordCount = 1)

        val result = repository.getItems(
            userId = "user-1",
            parentId = "folder-1",
            includeItemTypes = "Movie",
            sortBy = "SortName",
            sortOrder = "Ascending",
            startIndex = 0,
            limit = 20,
            genres = "Drama|Ação",
            years = "2023,2024",
            officialRatings = "PG-13|TV-MA",
            isPlayed = true,
            isFavorite = true,
        )

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) {
            api.getItems(
                userId = "user-1",
                parentId = "folder-1",
                includeItemTypes = "Movie",
                sortBy = "SortName",
                sortOrder = "Ascending",
                filters = null,
                searchTerm = null,
                startIndex = 0,
                limit = 20,
                genres = "Drama|Ação",
                years = "2023,2024",
                officialRatings = "PG-13|TV-MA",
                isPlayed = true,
                isFavorite = true,
            )
        }
    }

    @Test
    fun getItem_returnsMappedDomainItem() = runTest {
        coEvery { api.getItem("user-1", "movie-1") } returns sampleDto

        val result = repository.getItem("user-1", "movie-1")

        assertTrue(result.isSuccess)
        assertEquals("Inception", result.getOrThrow().name)
    }

    @Test
    fun getSimilarItems_returnsMappedList() = runTest {
        coEvery { api.getSimilarItems("movie-1", "user-1", 6) } returns BaseItemDtoQueryResultDto(
            items = listOf(sampleDto),
            totalRecordCount = 1
        )

        val result = repository.getSimilarItems("user-1", "movie-1", 6)

        assertTrue(result.isSuccess)
        assertEquals(1, result.getOrThrow().size)
    }

    @Test
    fun markAsPlayed_callsApiSuccessfully() = runTest {
        coEvery { api.markAsPlayed("user-1", "movie-1") } returns UserItemDataDto(played = true)

        val result = repository.markAsPlayed("user-1", "movie-1")

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.markAsPlayed("user-1", "movie-1") }
    }

    @Test
    fun markAsFavorite_callsApiSuccessfully() = runTest {
        coEvery { api.markAsFavorite("user-1", "movie-1") } returns UserItemDataDto(isFavorite = true)

        val result = repository.markAsFavorite("user-1", "movie-1")

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.markAsFavorite("user-1", "movie-1") }
    }
}
